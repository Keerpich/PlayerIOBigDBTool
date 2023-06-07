using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp.Common;
using AutoPlayerIO;
using DreamTeam.Tson;
using Konsole;
using PlayerIOClient;
using PlayerIOExportTool.Rules;
using Serilog;
using Serilog.Core;
using AutoPIO = AutoPlayerIO.PlayerIO;
using Connection = AutoPlayerIO.Connection;
using VenturePIO = PlayerIOClient.PlayerIO;

namespace PlayerIOExportTool
{
    class Program
    {
        public static string CreateSharedSecret(string username, string gameId)
        {
            using (var managed = SHA256.Create())
            {
                return BitConverter.ToString(managed.ComputeHash(Encoding.UTF8.GetBytes(username + gameId + Guid.NewGuid().ToString()))).Replace("-", string.Empty);
            }
        }
        
        /// <summary>
        /// A tool to assist with exporting a Player.IO game.
        /// </summary>
        /// <param name="username"> The username of your Player.IO account </param>
        /// <param name="password"> The password of your Player.IO account </param>
        /// <param name="gameId"> The ID of the game to export. For example: tictactoe-vk6aoralf0yflzepwnhdvw </param>
        /// <param name="action"> The intended action to be run. Valid values: "pull-from-server", "push-to-server", "run-rules"</param>
        /// <param name="skipConfirmation">Set to true to skip confirmation at the end of the script</param>
        static async Task Main(string username, string password, string gameId, string action, string skipConfirmation)
        {
            #region header
            Console.WriteLine(@"
                ╔═╗┬  ┌─┐┬ ┬┌─┐┬─┐ ╦╔═╗      
                ╠═╝│  ├─┤└┬┘├┤ ├┬┘ ║║ ║      
                ╩  ┴─┘┴ ┴ ┴ └─┘┴└─o╩╚═╝      
                      ╔╦╗┌─┐┌─┐┬  
                       ║ │ ││ ││  
                       ╩ └─┘└─┘┴─┘
            =================================");
            Console.WriteLine();
            #endregion
            
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("en-US");
            
            Parameters.CheckParameters(ref username, ref password, ref gameId, ref action, ref skipConfirmation);
            
            bool shouldSkipConfirmation = Boolean.Parse(skipConfirmation);

            var log = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();
            
            log.Information($"Action requested: {action}");
            
            while (true)
            {
                try
                {
                    switch (action)
                    {
                        case "push-to-server":
                            Client pushClient = await Authenticate(username, password, gameId, log);
                            await ActionPushToServer(log, pushClient);
                            break;

                        case "pull-from-server":
                            Client pullClient = await Authenticate(username, password, gameId, log);
                            await ActionPullFromServer(log, pullClient);
                            break;

                        case "run-rules":
                            ActionRunRules(log);
                            break;

                        case "quit":
                            if (!shouldSkipConfirmation)
                            {
                                Console.WriteLine();
                                log.Information(
                                    "The process has completed successfully. You can now close the program.");
                                Console.ReadLine();
                                Environment.Exit(0);
                            }

                            break;

                        default:
                            log.Error($"Invalid action. Action provided \"{action}\"");
                            return;
                    }
                }
                catch (Exception ex)
                {
                    log.Error(ex.Message);
                }

                Console.WriteLine();
                action = Parameters.AskForAction();
            }
        }

        private static async Task ActionPullFromServer(Logger log, Client client)
        {
            log.Information($"Exporting BigDB using layout from Pull-Settings");
            TableLayout pullTableLayout = new TableLayout();
            pullTableLayout.Load("Pull-Settings");
            await PullFromPlayerIO(client, pullTableLayout, "Pull-Data");
        }

        private static void ActionRunRules(Logger log)
        {
            TableLayout tableLayout = new TableLayout();
            tableLayout.Load("Push-Settings");
            var tablesWrapper = LoadLocalData(tableLayout);
            LoadAndRunRules("Push-Settings", tablesWrapper, log);
        }

        private static async Task ActionPushToServer(Logger log, Client client)
        {
            //Load local data
            log.Information("Pushing data to BigDB");
            TableLayout localTableLayout = new TableLayout();
            localTableLayout.Load("Push-Settings");
            var tablesWrapper = LoadLocalData(localTableLayout);
            List<string> tablesToPush = GetTablesToPush(tablesWrapper.GetTableNames());
            
            //Validate data
            LoadAndRunRules("Push-Settings", tablesWrapper, log);
                    
            //Purge
            log.Information("Starting data purge in BigDB");
            await PurgeTables(client, localTableLayout, tablesToPush);
            
            //Push
            await PushToPlayerIO(client, tablesWrapper, tablesToPush);
        }

        private static List<string> GetTablesToPush(List<string> validTables)
        {
            List<string> selectedTables = new List<string>();

            while (true)
            {
                for (int i = 0; i < validTables.Count; ++i)
                {
                    bool selected = selectedTables.Contains(validTables[i]);
                    char selectedChar = selected ? 'X' : ' ';
                    Console.WriteLine($"{i}. {validTables[i]} [{selectedChar}]");
                }
                Console.WriteLine();
                Console.WriteLine("Press enter to confirm choice or enter the index to (un)select a table");
                string choice = Console.ReadLine();

                if (string.IsNullOrEmpty(choice))
                    return selectedTables;
                
                int choiceIndex = Int32.Parse(choice);
                string chosenTable = validTables[choiceIndex];

                if (selectedTables.Contains(chosenTable))
                {
                    selectedTables.Remove(chosenTable);
                }
                else
                {
                    selectedTables.Add(chosenTable);
                }
            }
        }

        private static TablesWrapper LoadLocalData(TableLayout tableLayout)
        {
            var tableNames = tableLayout.GetTableNames();
            TablesWrapper tablesWrapper = new TablesWrapper();

            //create tables
            foreach (var tableName in tableNames)
            {
                var tableClassMembers = tableLayout.GetTableClassMembers(tableName);
                var objectFields = tableLayout.GetObjectFields(tableName);
                var objectDefinitions = tableLayout.GetObjectDefinitions(tableName);
                
                ClassBuilder.CreateClassesFromTable(tableName, tableClassMembers, objectFields, objectDefinitions, out ClassRegistry classRegistry);

                tablesWrapper.CreateTable(tableName, classRegistry.GetDotNetType(tableName));
                tablesWrapper.ReadTable(tableName);
            }

            return tablesWrapper;
        }

        private static void LoadAndRunRules(string folder, TablesWrapper tablesWrapper, Logger log)
        {
            log.Information("Running rules...");
            
            using (StreamReader reader = new StreamReader(Path.Combine(folder, "rules.csv")))
            {
                // Ignore header
                reader.ReadLine();

                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var ruleData = line.Split(",").ToList();

                    log.Information($"Running rule: {line}");
                    ARule rule = CreateRule(ruleData[0], ruleData.GetRange(1, ruleData.Count - 1));
                    tablesWrapper.RunRule(rule);
                }
            }
        }

        private static ARule CreateRule(string ruleType, List<string> ruleData)
        {
            string checkedField = ruleData[0];
            
            if (ruleType == "table")
            {
                string sourceField = ruleData[1];
                return new TableMappingMappingRule(checkedField, sourceField);
            }
            else if (ruleType == "values")
            {
                List<string> validValues = new List<string>(ruleData.GetRange(1, ruleData.Count - 1));
                return new ValuesMappingMappingRule(checkedField, validValues);
            }
            else if (ruleType == "interval")
            {
                int min = Int32.Parse(ruleData[1]);
                int max = Int32.Parse(ruleData[2]);
                return new IntervalRule(checkedField, min, max);
            }
            else if (ruleType == "unique")
            {
                return new UniqueRule(checkedField);
            }
            else
            {
                throw new DataException($"Found invalid rule type: {ruleType}");
            }

        }
        
        private static async Task PullFromPlayerIO(Client client, TableLayout tableLayout, string outputDirectory)
        {
            var tableNames = tableLayout.GetTableNames();

            TablesWrapper tablesWrapper = new TablesWrapper();

            var tasks = new List<Task>();

            foreach (var tableName in tableNames)
            {
                var tableLayoutData = tableLayout.GetTableLayoutData(tableName);
                var tableClassMembers = tableLayout.GetTableClassMembers(tableName);
                var objectFields = tableLayout.GetObjectFields(tableName);
                var objectDefinitions = tableLayout.GetObjectDefinitions(tableName);
                
                ClassBuilder.CreateClassesFromTable(tableName, tableClassMembers, objectFields, objectDefinitions, out ClassRegistry classRegistry);
                
                tablesWrapper.CreateTable(tableName, classRegistry.GetDotNetType(tableName));

                PullDataRunner pullDataRunner = 
                    new PullDataRunner(client, outputDirectory, tableName, tablesWrapper, tableClassMembers, tableLayoutData);

                var task = pullDataRunner.Run();
                
                tasks.Add(task);
            }

            await Task.WhenAll(tasks);
        }

        private static async Task PushToPlayerIO(Client client, TablesWrapper tablesWrapper, List<string> tableNames)
        {
            var progressBars = new Dictionary<string, ProgressBar>();
            progressBars.Add("_", new ProgressBar(PbStyle.DoubleLine, tableNames.Count));
            int tablesPushed = 0;

            //create tables
            foreach (var tableName in tableNames)
            {
                progressBars["_"].Refresh(tablesPushed, $"Pushing Tables - {tableName}");

                progressBars.Add(tableName, new ProgressBar(PbStyle.DoubleLine, tablesWrapper.GetNumberOfEntries(tableName)));
                
                await tablesWrapper.PopulateTable(client.BigDB, tableName, progressBars[tableName]);
                tablesPushed++;
                
                progressBars["_"].Refresh(tablesPushed, $"Pushing Tables - {tableName}");
            }
        }

        private static async Task PurgeTables(Client client, TableLayout localTableLayout,
            IEnumerable<string> tableNames)
        {
            int tablesPurged = 0;
            
            foreach (var tableName in tableNames)
            {
                var tableLayoutData = localTableLayout.GetTableLayoutData(tableName);
                var _tableName = tableName;
                client.BigDB.DeleteRange(tableName, tableLayoutData.Index, null, null, null, 
                        () => 
                        {
                            Interlocked.Increment(ref tablesPurged);
                        },
                        (error) =>
                        {
                            throw new Exception($"Couldn't purge table {_tableName}. Reason: {error.Message}");
                        });
            }

            while (tablesPurged < tableNames.Count())
            {
                await Task.Delay(1000);
            }
        }

        private static async Task<Client> Authenticate(string username, string password, string gameId, Logger log)
        {
            DeveloperAccount developer;
            DeveloperGame game;
            Client client = null;
            const string connectionName = "tool";
            
            // attempt to login and select game
            try
            {
                developer = await AutoPIO.LoginAsync(username, password);
                log.Information("Signed in as: " + developer.Username + " (" + developer.Email + ")");
            }
            catch
            {
                throw new AuthenticationException("Unable to authenticate. The login details provided were invalid.");
            }

            try
            {
                game = Enumerable.FirstOrDefault<DeveloperGame>(developer.Games, arg => arg.GameId == gameId);
                log.Information("Selected game: " + game.Name + " (" + game.GameId + ")");
            }
            catch
            {
                log.Error("Unable to authenticate. No game was found matching the specified gameId.");
                return client;
            }

            // delete export connection if already exists
            while (true)
            {
                var connections = await game.LoadConnectionsAsync();
            
                if (Enumerable.All<Connection>(connections, c => c.Name != connectionName))
                    break;
            
                log.Information(
                    "An existing tool connection was found - attempting to recreate it. This process should only take a few seconds.");
            
                await game.DeleteConnectionAsync(Enumerable.First<Connection>(connections, c => c.Name == connectionName));
            
                // wait a second (we don't want to spam)
                await Task.Delay(1000);
            }

            var sharedSecret = CreateSharedSecret(username, game.GameId);
            var tables = (await game.LoadBigDBAsync()).Tables;
            
            log.Information("Now attempting to create tool connection with shared_secret = " + sharedSecret);
            
            await game.CreateConnectionAsync(connectionName, "A connection with access to all BigDB tables - used for the tool.",
                DeveloperGame.AuthenticationMethod.BasicRequiresAuthentication, "Default",
                Enumerable.Select<Table, (Table t, bool, bool, bool, bool, bool, bool)>(tables, t => (t, true, true, true, true, true, true)).ToList(), sharedSecret);

            // ensure the export connection exists before continuing
            while (true)
            {
                var connections = await game.LoadConnectionsAsync();

                if (Enumerable.Any<Connection>(connections, c => c.Name == connectionName))
                    break;

                log.Information("Waiting until we have confirmation that the tool connection exists...");
                Thread.Sleep(1000); // we don't want to spam.
            }

            log.Information("The tool connection has been created.");

            // connect to the game and start export process.
            try
            {
                client = VenturePIO.Connect(game.GameId, connectionName, "user", VenturePIO.CalcAuth256("user", sharedSecret));
            }
            catch (Exception ex)
            {
                log.Error("Unable to authenticate. An error occurred while trying to authenticate with the export connection. Details: " +
                          ex.Message);
                return client;
            }

            return client;
        }
    }
}
