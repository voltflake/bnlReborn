using BNLReloadedServer;
using BNLReloadedServer.BaseTypes;
using BNLReloadedServer.ControlPanel;
using BNLReloadedServer.Database;
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

Console.SetOut(new BroadcastingTextWriter(Console.Out));

var configs = Databases.ConfigDatabase;
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
    // Fetch first: touching Databases.Catalogue is what constructs it, so doing that only
    // once the cards are in hand means it is never observable empty. A CouchDB failure here
    // takes the process down before any singleton exists.
    var loadedCards = catalogueStore.Load();
    ((ServerCatalogue)Databases.Catalogue).Replicate(loadedCards);
    Console.WriteLine($"Replicated {loadedCards.Count} cards to server catalogue");
}

if (runServer)
{
    MasterServer? server = null;
    if (masterMode)
    {
        // Create a new TCP server
        server = new MasterServer(configs.MasterIp(), 28100);
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

    // Follow CouchDB's _changes feed so a card edited anywhere shows up here without a restart.
    var watcherCts = new CancellationTokenSource();
    ShutdownSignal.WaitForShutdown.ContinueWith(_ => watcherCts.Cancel());
    new CouchChangesWatcher(
        configs.CouchDbEndpoint(),
        configs.CouchDbDatabaseName(),
        configs.CouchDbCredentials(),
        () =>
        {
            var newCardList = catalogueStore.Load();
            if (Databases.Catalogue is not ServerCatalogue serverCatalogue) return;
            serverCatalogue.Replicate(newCardList);
            new ServiceCatalogue(new ServerSender(regionServer)).SendReplicate(newCardList);
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
    
    Console.WriteLine("Press Enter to stop the server or '!' to restart the server...");
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
                        Console.Write("Server restarting...");
                        server?.Restart();
                        regionServer.Restart();
                        regionClient.Disconnect();
                        regionClient.Reconnect();
                        matchServer.Restart();
                        Console.WriteLine("Done!");
                        break;
                    }
                    case "refreshCdbLoad" when Databases.Catalogue is ServerCatalogue serverCatalogue:
                    {
                        Console.Write("Refreshing cdb...");
                        try
                        {
                            var newCardList = catalogueStore.Load();
                            serverCatalogue.Replicate(newCardList);
                            var catalogueReplicator = new ServiceCatalogue(new ServerSender(regionServer));
                            catalogueReplicator.SendReplicate(newCardList);
                            Console.WriteLine("Done!");
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"Failed - {e.Message}");
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
        Console.Write("Server stopping...");
        server?.Stop();
        regionServer.Stop();
        regionClient.DisconnectAndStop();
        if (configs.IsMaster())
        {
            Databases.MasterServerDatabase.RemoveRegionServer("master");
        }
        matchServer.Stop();
        controlPanel?.Dispose();
        Console.WriteLine("Done!");
    }
}
