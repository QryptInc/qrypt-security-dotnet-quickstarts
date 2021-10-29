// <Create a chat client>
using Azure;
using Azure.Communication;
using Azure.Communication.Chat;
using Azure.Communication.Identity;
using System;
using System.Threading.Tasks;

namespace ChatQuickstart
{
    class Program
    {
        private const string COMMUNICATION_SERVICES_ENDPOINT = "COMMUNICATION_SERVICES_ENDPOINT";
        private const string COMMUNICATION_SERVICES_ACCESS_KEY = "COMMUNICATION_SERVICES_ACCESS_KEY";
        private const string COMMUNICATION_SERVICES_IDENTITY_ALICE = "COMMUNICATION_SERVICES_IDENTITY_ALICE";
        private const string COMMUNICATION_SERVICES_IDENTITY_BOB = "COMMUNICATION_SERVICES_IDENTITY_BOB";
        private const string COMMUNICATION_SERVICES_CHAT_THREAD_ID = "COMMUNICATION_SERVICES_CHAT_THREAD_ID";


        static async Task Main(string[] args)
        {
            if (args.Length == 0 ||
                (args[0] != "alice" && 
                args[0] != "bob"))
            {
                System.Console.WriteLine("Please enter 'alice' or 'bob' as an argument.");
                return;
            }

            bool isAlice = true;
            if (args[0] == "bob")
            {
                isAlice = false;
            }

            // The following ACS endpoint/access key environment variables are assumed to exist
            string endpoint = Environment.GetEnvironmentVariable(COMMUNICATION_SERVICES_ENDPOINT, EnvironmentVariableTarget.User);
            string accessKey = Environment.GetEnvironmentVariable(COMMUNICATION_SERVICES_ACCESS_KEY, EnvironmentVariableTarget.User);
            if (endpoint == null ||
                accessKey == null)
            {
                Console.WriteLine("The user environment variables COMMUNICATION_SERVICES_ENDPOINT and COMMUNICATION_SERVICES_ACCESSKEY must be setup for your Azure Communication Service.");
                return;
            }
            Uri endpointUri = new Uri(endpoint);

            if (isAlice)
            {
                // Alice creates new ACS identities and ennvironment variables to represent for each run
                var identityAlice = await CreateIdentity(endpointUri, accessKey, COMMUNICATION_SERVICES_IDENTITY_ALICE);
                var identityBob = await CreateIdentity(endpointUri, accessKey, COMMUNICATION_SERVICES_IDENTITY_BOB);

                // Alice's ACS access token, which expires in 24 hours, is created for each run
                string tokenAlice = await CreateAccessToken(endpointUri, accessKey, identityAlice);

                // Alice creates an ACS chat client
                CommunicationTokenCredential communicationTokenCredential = new CommunicationTokenCredential(tokenAlice);
                ChatClient chatClient = new ChatClient(endpointUri, communicationTokenCredential);

                // Alice starts an ACS chat thread
                var chatParticipant = new ChatParticipant(identifier: identityAlice)
                {
                    DisplayName = "Alice"
                };
                CreateChatThreadResult createChatThreadResult = await chatClient.CreateChatThreadAsync(topic: "Hello world!", participants: new[] { chatParticipant });

                // Store the chat thread id away so we can access when we run as Bob
                Environment.SetEnvironmentVariable(COMMUNICATION_SERVICES_CHAT_THREAD_ID, createChatThreadResult.ChatThread.Id, EnvironmentVariableTarget.User);

                // Alice gets an ACS chat thread client
                ChatThreadClient chatThreadClient = chatClient.GetChatThreadClient(threadId: createChatThreadResult.ChatThread.Id);
                string threadId = chatThreadClient.Id;

                // Alice adds Bob as a participant to the ACS chat thread
                var participants = new[]
                {
                    new ChatParticipant(identityBob) { DisplayName = "Bob" },
                };
                await chatThreadClient.AddParticipantsAsync(participants: participants);

                // Alice sends a message to the ACS chat thread
                var qryptHelper = new QryptHelper();
                var cipherText = qryptHelper.SendMessage("hello world");
                SendChatMessageResult sendChatMessageResult = await chatThreadClient.SendMessageAsync(content: cipherText, type: ChatMessageType.Text);
                string messageId = sendChatMessageResult.Id;
            }
            else
            {
                // Bob retrieves his ACS identity created by Alice for this run
                var identityId = Environment.GetEnvironmentVariable(COMMUNICATION_SERVICES_IDENTITY_BOB, EnvironmentVariableTarget.User);
                var identityBob = new CommunicationUserIdentifier(identityId);
            
                // Bob's ACS access token, which expires in 24 hours, is created for this run
                string tokenBob = await CreateAccessToken(endpointUri, accessKey, identityBob);

                // Bob creates an ACS chat client
                CommunicationTokenCredential communicationTokenCredential = new CommunicationTokenCredential(tokenBob);
                ChatClient chatClient = new ChatClient(endpointUri, communicationTokenCredential);

                // Bob gets the ACS chat thread client based on the chat thread id Alice created above
                string threadId = Environment.GetEnvironmentVariable(COMMUNICATION_SERVICES_CHAT_THREAD_ID, EnvironmentVariableTarget.User);
                ChatThreadClient chatThreadClient = chatClient.GetChatThreadClient(threadId: threadId);

                // Bob receives chat messages from the ACS chat thread
                AsyncPageable<ChatMessage> allMessages = chatThreadClient.GetMessagesAsync();
                var qryptHelper = new QryptHelper();
                await foreach (ChatMessage message in allMessages)
                {
                    if (message.Content.Message != null)
                    {
                        var plainText = qryptHelper.RecvMessage(message.Content.Message);
                        Console.WriteLine(plainText);
                    }
                }
            }
        }

        private static async Task<CommunicationUserIdentifier> CreateIdentity(Uri endpointUri, string accessKey, string envVar)
        {
            CommunicationUserIdentifier identity = null;

            var client = new CommunicationIdentityClient(endpointUri, new AzureKeyCredential(accessKey));
            var identityResponse = await client.CreateUserAsync();
            identity = identityResponse.Value;
            Environment.SetEnvironmentVariable(envVar, identity.Id, EnvironmentVariableTarget.User);

            return identity;
        }

        private static async Task<string> CreateAccessToken(Uri endpointUri, string accessKey, CommunicationUserIdentifier identity)
        {
            var client = new CommunicationIdentityClient(endpointUri, new AzureKeyCredential(accessKey));
            var tokenResponse = await client.GetTokenAsync(identity, scopes: new[] { CommunicationTokenScope.Chat });
            var token = tokenResponse.Value.Token;

            return token;
        }
    }
}