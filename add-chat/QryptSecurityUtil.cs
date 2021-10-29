using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Qrypt.Security;
using Qrypt.Security.Native;

 
namespace ChatQuickstart
{
    public struct User 
    {
        /// <summary>
        /// Unique ID
        /// </summary>
        public string UserID;

        /// <summary>
        /// Cache configuration
        /// </summary>
        public CacheConfig CacheConfig;

        /// <summary>
        /// Session provider
        /// </summary>
        public SessionProvider SessionProvider;

        /// <summary>
        /// Cache manager
        /// </summary>
        public CacheManager CacheManager;

        /// <summary>
        /// Key service
        /// </summary>
        public IKeyService KeyService;

        /// <summary>
        /// Random sources
        /// </summary>
        public List<IRandomSource> RandomSources;

        /// <summary>
        /// Locations
        /// </summary>
        public List<ILocation> Locations;


        /// <summary>
        /// QDEA sources
        /// </summary>
        public List<IQDEASource> QDEASources;       
    }


    public class QryptHelper
    {
        private static string _InMemoryFilePath = "qryptlib.db";
        public static string _TestLogPath = "testLogPath/qryptlib.log";

        private TestQDEAServerCluster _ServerCluster;
        private User _AliceUser;
        private User _BobUser;

        public const ulong KB = 1024;
        public const ulong MB = 1024 * KB;
        public const ulong GB = 1024 * MB;
        public const ulong TB = 1024 * GB;

        public void Setup()
        {
            // Initialize QDEA server cluster
            _ServerCluster = CreateTestQDEAServerClusterWithoutPools();

            // Setup users
            _AliceUser = UserInit(_ServerCluster);
            _BobUser = UserInit(_ServerCluster);
        }

        public string SendMessage(string plainText) 
        {
            Setup();
            InitializeCacheManagers();

            UserBeginSession(_AliceUser);
            UserBeginSession(_BobUser);

            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText).ToList<byte>();

            var cipherText = _AliceUser.SessionProvider.EncryptMessage(
                _AliceUser.UserID, 
                plainTextBytes,
                plainTextBytes.Count);

            UserEndSession(_AliceUser);
            UserEndSession(_BobUser);

            return Convert.ToBase64String(cipherText.ToArray());
        }

        public string RecvMessage(string cipherText) 
        {
            Setup();
            InitializeCacheManagers();

            UserBeginSession(_AliceUser);
            UserBeginSession(_BobUser);

            var cipherTextBytes = Convert.FromBase64String(cipherText).ToList<byte>();

            var plainTextBytes = _BobUser.SessionProvider.DecryptMessage(
                _BobUser.UserID, 
                cipherTextBytes,
                cipherTextBytes.Count);

            string plainText = System.Text.Encoding.UTF8.GetString(plainTextBytes.ToArray());

            UserEndSession(_AliceUser);
            UserEndSession(_BobUser);

            return plainText;
        }

        private void InitializeCacheManagers() 
        {
            _AliceUser.CacheManager.Initialize(_AliceUser.CacheConfig, _AliceUser.RandomSources, _AliceUser.Locations,
                                                _AliceUser.QDEASources);
            _BobUser.CacheManager.Initialize(_BobUser.CacheConfig, _BobUser.RandomSources, _BobUser.Locations,
                                                _BobUser.QDEASources);

            _AliceUser.CacheManager.PerformCacheMaintenance();
            _BobUser.CacheManager.PerformCacheMaintenance();
        }

        private static void UserBeginSession(User user) 
        {
            user.SessionProvider.BeginSession(user.UserID, _InMemoryFilePath, user.CacheManager);
        }

        public static void UserEndSession(User user) 
        {
            user.SessionProvider.EndSession(); 
        }

        private static User UserInit(TestQDEAServerCluster serverCluster)
        {
            ulong availableSize = 10 * GB;
            uint maxTTD = 60 * 60 * 24 * 7;
            User user = new User();
            
            // Generate peerID
            user.UserID = System.Guid.NewGuid().ToString();

            // Construct key service and session provider
            user.KeyService = new TestKeyService();
            user.SessionProvider = SessionProvider.CreateSessionProvider(user.KeyService, ratchet_cka_algo.KYBER1024);

            // Construct cache manager
            user.CacheManager = CacheManager.CreateCacheManager();

            // Initialize cache manager
            user.RandomSources = new List<IRandomSource>();
            CreateNTestSources(1, user.RandomSources);

            string cacheManagerDir = "CacheManager" + user.UserID;
            user.Locations = new List<ILocation>();
            CreateTestLocation(user.Locations, cacheManagerDir, availableSize);

            user.QDEASources = new List<IQDEASource>();
            CreateTestQDEASource(serverCluster, user.QDEASources);

            user.CacheConfig = InitTestCacheConfig(user.Locations[0].GetID(), maxTTD, false);

            return user;
        }

        private static void CreateNTestSources(uint numSources, List<IRandomSource> randomSources) 
        {
            for (uint i = 0; i < numSources; i++) 
            {
                var eaasConfig = new EaaSConfig() 
                {
                    ApiEndPoint = "api-eus.qrypt.com",
                    Token = "dummy_token", // Environment.GetEnvironmentVariable("EAAS_TOKEN");
                    LogPath = _TestLogPath,
                    CertPath = "",
                };

                var sourceObjID = System.Guid.NewGuid().ToString();
                var source = new TestEaaSSource(sourceObjID, eaasConfig);
                randomSources.Add(source);
            }
        }

        private static void CreateTestQDEASource(TestQDEAServerCluster serverCluster, List<IQDEASource> qdeaSources) 
        {
            var qdeaConfig = new QDEADirectoryConfig();
            var sourceObjID = System.Guid.NewGuid().ToString();
            qdeaConfig.ServerURL = "SomeURL";
            qdeaConfig.Token = "SomeToken";
            var source = new TestQDEASource(sourceObjID, qdeaConfig, serverCluster);
            qdeaSources.Add(source);
        }

        public static void CreateTestLocation(List<ILocation> locations, string cacheManagerDir, ulong spaceAvailable) 
        {
            var locationConfig = new LocationConfig
            {
                Id = System.Guid.NewGuid().ToString(),
                SpaceAvailable = spaceAvailable,
                Path = cacheManagerDir,
                StorageType = LocationStorageType.LOCATION_ONDEVICE
            };

            var location = new FileLocation(locationConfig);

            locations.Add(location);
                
            RemoveDir(locationConfig.Path);
            CreateDir(locationConfig.Path);
        }

        private static CacheConfig InitTestCacheConfig(string locationObjID, uint maxTTD, bool enableInlineMaintenance) 
        {
            CacheConfig cacheConfig = new CacheConfig
            {
                DeviceSecret = System.Text.Encoding.UTF8.GetBytes("Password124").ToList<byte>(),
                EnableInlineMaintenance = enableInlineMaintenance,
                Mode = CacheMode.CACHEMODE_ONDEVICE_QRAND_INMEMORY_SAMPLES,
                MaxTTD = maxTTD,
                NumCachedRotations = 0,
                TargetPoolCapacity = 8 * MB,
                TargetMessageLength = (uint)(128 * KB),
                TargetNumMessages = 10,
                CacheStoreLocationObjID = locationObjID,
            };

            return cacheConfig;
        }

        private static TestQDEAServerCluster CreateTestQDEAServerClusterWithoutPools(bool useBadServers = true) 
        {
            var testConfig = new TestQDEAServerClusterConfig();
            testConfig.UseTestWithPool = false;
            testConfig.NumLogicalBlastServers = 10;
            testConfig.NumActiveLogicalBlastServers = 10;
            testConfig.NumFailStopServers = 0;
            testConfig.NumMaliciousServers = 0;
            testConfig.PoolSize = GB;
            
            var serverCluster = new TestQDEAServerCluster(testConfig);
            return serverCluster;
        }

        private static void CreateDir(string dirPath) 
        {
            try
            {
                Directory.CreateDirectory(dirPath);
            }
            catch(Exception ex)
            {
                Console.WriteLine("Failed to create directory: {0}", ex.ToString());
            }
        }

        private static void RemoveDir(string dirPath)
        {
            try
            {
                Directory.Delete(path: dirPath, recursive: true);
            }
            catch(Exception ex)
            {
                Console.WriteLine("Failed to remove directory: {0}", ex.ToString());
            }
        }
    }
}