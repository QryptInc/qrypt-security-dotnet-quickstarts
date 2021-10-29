using Qrypt.Security;
using System.Collections.Generic;
using System.Linq;

namespace ChatQuickstart
{
    public class TestKeyService : IKeyService 
    {
        public void GetRatchetSenderKeyPair(string peerID, List<byte> publicKey, List<byte> symmetricKey)
        {
            publicKey.AddRange(TestKeys.KYBER_PUBLIC_KEY.ToList<byte>());
            symmetricKey.AddRange(TestKeys.SYMMETRIC_KEY.ToList<byte>());
        }

        public void GetRatchetReceiverKeyPair(string peerID, List<byte> privateKey, List<byte> symmetricKey)
        {
            privateKey.AddRange(TestKeys.KYBER_PRIVATE_KEY.ToList<byte>());
            symmetricKey.AddRange(TestKeys.SYMMETRIC_KEY.ToList<byte>());
        }
    }
}