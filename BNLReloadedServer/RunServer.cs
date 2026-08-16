using BNLReloadedServer;
using BNLReloadedServer.BaseTypes;
using BNLReloadedServer.ControlPanel;
using BNLReloadedServer.Database;
using BNLReloadedServer.Logging;
using BNLReloadedServer.ProtocolHelpers;
using BNLReloadedServer.Servers;
using BNLReloadedServer.Service;
using CouchDB.Driver;
using CouchDB.Driver.Options;

if (args is ["--hash-password", var passwordToHash])
{
    Console.WriteLine(PasswordHasher.Hash(passwordToHash));
    return;
}

Log.Attach();

var configs = Databases.ConfigDatabase;
Log.MinLevel = configs.MinLogLevel();
var masterMode = configs.IsMaster();
var runServer = configs.DoRunServer();

const int bufferSize = 2000000;  // 2MB

var catalogueStore = new CouchCatalogueStore(
    new CouchClient(configs.CouchDbEndpoint(), configs.CouchDbCredentials(),
        new CouchClientOptions
        {
            JsonSerializerOptions = JsonHelper.DefaultSerializerSettings,
            ThrowOnQueryWarning = false
        }),
    configs.CouchDbDatabaseName(),
    Path.Combine(Databases.CacheFolderPath, configs.ExportCdbName()),
    JsonHelper.DefaultSerializerSettings);

if (runServer)
{
    var loadedCards = catalogueStore.Load();
    ((ServerCatalogue)Databases.Catalogue).Replicate(loadedCards);
    Log.Info(LogCat.Catalogue, $"Replicated {loadedCards.Count} cards to server catalogue");

    if (Databases.MapDatabase.GetMapIds().Count == 0)
    {
        Log.Error(LogCat.Map, $"No maps found in '{Path.Combine(Databases.BaseFolderPath, "Maps")}' — " +
                              "the server has nothing to put on a lobby ballot and would never start a match. Stopping.");
        return;
    }
}

if (runServer)
{
    MasterServer? server = null;
    if (masterMode)
    {
        // Create a new TCP server
        server = new MasterServer(configs.MasterIp(), 28100);
        server.OptionNoDelay = true;
        server.OptionSendBufferSize = bufferSize;
        server.OptionReceiveBufferSize = bufferSize;
        
        // Start the server
        server.Start();
    }

    var regionServer = new RegionServer(configs.RegionIp(), 28101);
    regionServer.OptionNoDelay = true;
    regionServer.OptionSendBufferSize = bufferSize;
    regionServer.OptionReceiveBufferSize = bufferSize;
    var regionClient = new RegionClient(configs.MasterHost(), 28100);
    regionClient.OptionNoDelay = true;
    regionClient.OptionSendBufferSize = bufferSize;
    regionClient.OptionReceiveBufferSize = bufferSize;
    var matchServer = new MatchServer(configs.RegionIp(), 28102);
    matchServer.OptionNoDelay = true;
    matchServer.OptionSendBufferSize = bufferSize;
    matchServer.OptionReceiveBufferSize = bufferSize;
    Databases.SetRegionDatabase(new RegionServerDatabase(regionServer, matchServer));
   
    regionServer.Start();
    regionClient.ConnectAsync();
    matchServer.Start();

    var watcherCts = new CancellationTokenSource();
    ShutdownSignal.WaitForShutdown.ContinueWith(_ => watcherCts.Cancel());
    new CouchChangesWatcher(
        configs.CouchDbEndpoint(),
        configs.CouchDbDatabaseName(),
        configs.CouchDbCredentials(),
        change =>
        {
            if (Databases.Catalogue is not ServerCatalogue serverCatalogue) return;
            var catalogueService = new ServiceCatalogue(new ServerSender(regionServer));

            if (change.Deleted)
            {
                if (serverCatalogue.RemoveCard(change.DocumentId))
                    catalogueService.SendRemoveCard(change.DocumentId);
                return;
            }

            var card = catalogueStore.LoadCard(change.DocumentId);
            if (card == null)
            {
                // CouchDB can contain design/configuration documents which are not cards.
                // If this id used to be a card, removing it is also the correct client update.
                if (serverCatalogue.RemoveCard(change.DocumentId))
                    catalogueService.SendRemoveCard(change.DocumentId);
                return;
            }

            serverCatalogue.UpdateCard(card);
            catalogueService.SendUpdateCard(card);
        }).Start(watcherCts.Token);

    ControlPanelServer? controlPanel = null;
    if (Databases.ConfigDatabase.ControlPanelEnabled())
    {
        var prefix = $"http://{Databases.ConfigDatabase.ControlPanelHost()}:{Databases.ConfigDatabase.ControlPanelPort()}/";
        controlPanel = new ControlPanelServer(
            prefix,
            server,
            regionServer,
            matchServer,
            catalogueStore,
            (ServerCatalogue)Databases.Catalogue);
        controlPanel.Start();
    }
    
    Log.Info(LogCat.Server, "Press Enter to stop the server or '!' to restart the server...");
    try
    {
        // Perform text input
        while (true)
        {
            if (Databases.ConfigDatabase.DoReadline())
            {
                var line = Console.ReadLine();
                if (string.IsNullOrEmpty(line))
                    break;

                switch (line)
                {
                    // Restart the server
                    case "!":
                    {
                        Log.Info(LogCat.Server, "Restarting listeners...");
                        server?.Restart();
                        regionServer.Restart();
                        regionClient.Disconnect();
                        regionClient.Reconnect();
                        matchServer.Restart();
                        Log.Info(LogCat.Server, "Listeners restarted");
                        break;
                    }
                    case "refreshCdbLoad" when Databases.Catalogue is ServerCatalogue serverCatalogue:
                    {
                        Log.Info(LogCat.Catalogue, "Refreshing catalogue...");
                        try
                        {
                            var newCardList = catalogueStore.Load();
                            serverCatalogue.Replicate(newCardList);
                            var catalogueReplicator = new ServiceCatalogue(new ServerSender(regionServer));
                            catalogueReplicator.SendReplicate(newCardList);
                            Log.Info(LogCat.Catalogue, $"Refreshed: {newCardList.Count} cards replicated to connected clients");
                        }
                        catch (Exception e)
                        {
                            Log.Error(LogCat.Catalogue, "Catalogue refresh failed, keeping the current catalogue", e);
                        }
                        break;
                    }
                }
            }
            else
            {
                await ShutdownSignal.WaitForShutdown;
                break;
            }
        }
    }
    finally
    {
        // Stop the server
        Log.Info(LogCat.Server, "Server stopping...");
        server?.Stop();
        regionServer.Stop();
        regionClient.DisconnectAndStop();
        if (configs.IsMaster())
        {
            Databases.MasterServerDatabase.RemoveRegionServer("master");
        }
        matchServer.Stop();
        controlPanel?.Dispose();
        Log.Info(LogCat.Server, "Server stopped");
    }
}
