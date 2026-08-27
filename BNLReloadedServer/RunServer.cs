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

Log.Attach();

var configs = Databases.ConfigDatabase;
Log.MinLevel = configs.MinLogLevel();
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

    var loadedCards = catalogueStore.Load();
    ((ServerCatalogue)Databases.Catalogue).Replicate(loadedCards);
    Log.Info(LogCat.Catalogue, $"Replicated {loadedCards.Count} cards to server catalogue");

    if (Databases.MapDatabase.GetMapIds().Count == 0)
    {
        Log.Error(LogCat.Map, $"No maps found in '{Path.Combine(Databases.BaseFolderPath, "Maps")}' — " +
                              "the server has nothing to put on a lobby ballot and would never start a match. Stopping.");
        return;
    }
    var server = new MasterServer(configs.MasterIp(), 28100);
    server.OptionNoDelay = true;
    server.OptionSendBufferSize = bufferSize;
    server.OptionReceiveBufferSize = bufferSize;
    server.Start();

    var regionServer = new RegionServer(configs.MasterIp(), 28101);
    regionServer.OptionNoDelay = true;
    regionServer.OptionSendBufferSize = bufferSize;
    regionServer.OptionReceiveBufferSize = bufferSize;
    var matchServer = new MatchServer(configs.MasterIp(), 28102);
    matchServer.OptionNoDelay = true;
    matchServer.OptionSendBufferSize = bufferSize;
    matchServer.OptionReceiveBufferSize = bufferSize;
    Databases.SetRegionDatabase(new RegionServerDatabase(regionServer, matchServer));
    // Master, region and instances are one process.  Register the local region directly instead
    // of opening a loopback TCP connection merely to exchange its own address and public key.
    Databases.MasterServerDatabase.AddRegionServer("master", configs.MasterPublicHost(), configs.GetRegionInfo());
   
    regionServer.Start();
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
        // The panel is often reached through a reverse proxy or an alternate DNS name.  Its
        // listener must accept those Host headers; route and session authorization still happen
        // inside ControlPanelServer.
        var prefix = $"http://*:{Databases.ConfigDatabase.ControlPanelPort()}/";
        controlPanel = new ControlPanelServer(
            prefix,
            server,
            regionServer,
            matchServer,
            catalogueStore,
            (ServerCatalogue)Databases.Catalogue);
        controlPanel.Start();
    }
    
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        // Keep Ctrl+C inside the normal shutdown path so listeners and the control panel are
        // disposed cleanly instead of relying on abrupt process termination.
        eventArgs.Cancel = true;
        ShutdownSignal.Request();
    };

    Log.Info(LogCat.Server, "Server running; press Ctrl+C to stop.");
    try
    {
        await ShutdownSignal.WaitForShutdown;
    }
    finally
    {
        // Stop the server
        Log.Info(LogCat.Server, "Server stopping...");
        server.Stop();
        regionServer.Stop();
        Databases.MasterServerDatabase.RemoveRegionServer("master");
        matchServer.Stop();
        controlPanel?.Dispose();
        Log.Info(LogCat.Server, "Server stopped");
    }
