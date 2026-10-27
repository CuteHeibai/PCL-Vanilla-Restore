using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;

namespace PCLRestore.Adaptation;
internal static class Hashing {
 internal static string Sha(byte[] bytes){using var hash=SHA256.Create();return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
}
internal static class JsonData {
 static DataContractJsonSerializer Serializer(Type type)=>new DataContractJsonSerializer(type,new DataContractJsonSerializerSettings{UseSimpleDictionaryFormat=true});
 internal static byte[] Write<T>(T value){using var stream=new MemoryStream();Serializer(typeof(T)).WriteObject(stream,value);return stream.ToArray();}
 internal static T Read<T>(byte[] value){using var stream=new MemoryStream(value);return (T)Serializer(typeof(T)).ReadObject(stream)!;}
}
[DataContract]
public sealed class AdaptationProfile {
 [DataMember]public string upstreamVersion="";
 [DataMember]public int patchRevision;
 [DataMember]public bool vanillaDownload,portableTemp,officialAutoRestore;
 [DataMember]public int updaterProtocol,adapterProtocol;
 [DataMember]public string sourceCommit="",sourceExeSha256="";
 [DataMember]public Dictionary<string,string> methodFingerprints=new Dictionary<string,string>();
}
[DataContract]
internal sealed class CompatibilityContract {
 [DataMember]public string sourceSha256="",version="";
 [DataMember]public Dictionary<string,string> methods=new Dictionary<string,string>();
}
