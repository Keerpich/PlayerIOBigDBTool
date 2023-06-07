using System;

namespace PlayerIOExportTool
{
    public static class Parameters
    {
        private const string defaultGameId = "<YOUR GAME ID>";
        private static readonly string[] ValidActions = {"pull-from-server", "push-to-server", "run-rules", "quit"}; 

        public static void CheckParameters(ref string username, ref string password, ref string gameId, ref string action, ref string skipConfirmation)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(gameId) || string.IsNullOrEmpty(action))
            {
                Console.WriteLine("Arguments can also be passed when running the script. Use \"-h\" for more information.");
            }
            
            if (string.IsNullOrEmpty(username))
            {
                username = AskForUsername();
            }

            if (string.IsNullOrEmpty(password))
            {
                password = AskForPassword();
            }

            if (string.IsNullOrEmpty(gameId))
            {
                gameId = AskForGameId();
            }

            if (string.IsNullOrEmpty(action))
            {
                action = AskForAction();
            }

            if (string.IsNullOrEmpty(skipConfirmation))
            {
                skipConfirmation = "false";
            }
        }

        private static string AskForUsername()
        {
            Console.WriteLine("Please enter your username: ");
            var username = Console.ReadLine();
            return username;
        }

        private static string AskForPassword()
        {
            Console.WriteLine("Please enter your password: ");
            var password = Console.ReadLine();
            return password;
        }

        private static string AskForGameId()
        {
            Console.WriteLine($"Please enter the Game ID (leave blank for \"{defaultGameId}\")");
            var gameId = Console.ReadLine();
            if (string.IsNullOrEmpty(gameId))
            {
                gameId = defaultGameId;
            }

            return gameId;
        }

        public static string AskForAction()
        {
            Console.WriteLine($"Please select the intended action by typing the number: ");

            for (int i = 0; i < ValidActions.Length; ++i)
            {
                Console.WriteLine($"{i}. {ValidActions[i]}");
            }

            string choice = Console.ReadLine();
            int choiceIndex = Int32.Parse(choice);

            return ValidActions[choiceIndex];
        }

    }
}