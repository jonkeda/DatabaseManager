using System.IO;
using System.Linq;
using System.Xml.Linq;
using Databases.Model.Connection;

namespace Databases.Converter.Profile
{
    public class DataTransferErrorProfileManager
    {
        private static readonly object obj = new object();
        public static string ProfileFolder { get; set; } = "Profiles";

        public static string ProfilePath => Path.Combine(ProfileFolder, "DataTransferError.xml");

        public static bool Save(DataTransferErrorProfile profile)
        {
            if (!Directory.Exists(ProfileFolder))
            {
                Directory.CreateDirectory(ProfileFolder);
            }

            var filePath = ProfilePath;
            if (!File.Exists(filePath))
            {
                using (var sw = File.CreateText(filePath))
                {
                    sw.WriteLine(
                        @"<?xml version=""1.0"" encoding=""utf-8""?>
<Config>
</Config>
");
                    sw.Flush();
                }
            }

            lock (obj)
            {
                var doc = XDocument.Load(filePath);
                var root = doc.Root;

                var profileElement = root.Elements("Item").FirstOrDefault(item =>
                    item.Attribute("SourceServer")?.Value == profile.SourceServer &&
                    item.Attribute("SourceDatabase")?.Value == profile.SourceDatabase &&
                    item.Attribute("TargetServer")?.Value == profile.TargetServer &&
                    item.Attribute("TargetDatabase")?.Value == profile.TargetDatabase
                );

                if (profileElement == null)
                {
                    profileElement = new XElement("Item",
                        new XAttribute("SourceServer", profile.SourceServer),
                        new XAttribute("SourceDatabase", profile.SourceDatabase),
                        new XAttribute("SourceTableName", profile.SourceTableName),
                        new XAttribute("TargetServer", profile.TargetServer),
                        new XAttribute("TargetDatabase", profile.TargetDatabase),
                        new XAttribute("TargetTableName", profile.TargetTableName)
                    );
                    root.Add(profileElement);
                }
                else
                {
                    profileElement.Attribute("SourceTableName").Value = profile.SourceTableName;
                    profileElement.Attribute("TargetTableName").Value = profile.TargetTableName;
                }

                doc.Save(filePath);
            }

            return true;
        }

        public static bool Remove(DataTransferErrorProfile profile)
        {
            var filePath = ProfilePath;
            if (!File.Exists(filePath))
            {
                return false;
            }

            var doc = XDocument.Load(filePath);
            var root = doc.Root;

            var profileElement = root.Elements("Item").FirstOrDefault(item =>
                item.Attribute("SourceServer")?.Value == profile.SourceServer &&
                item.Attribute("SourceDatabase")?.Value == profile.SourceDatabase &&
                item.Attribute("TargetServer")?.Value == profile.TargetServer &&
                item.Attribute("TargetDatabase")?.Value == profile.TargetDatabase
            );

            if (profileElement != null)
            {
                profileElement.Remove();
                doc.Save(filePath);
                return true;
            }

            return false;
        }

        public static DataTransferErrorProfile GetProfile(ConnectionInfo sourceConnectionInfo,
            ConnectionInfo targetConnectionInfo)
        {
            DataTransferErrorProfile profile = null;
            var filePath = ProfilePath;
            if (!File.Exists(filePath) || sourceConnectionInfo == null || targetConnectionInfo == null)
            {
                return null;
            }

            var doc = XDocument.Load(filePath);
            var root = doc.Root;

            var profileElement = root.Elements("Item").FirstOrDefault(item =>
                item.Attribute("SourceServer")?.Value == sourceConnectionInfo.Server &&
                item.Attribute("SourceDatabase")?.Value == sourceConnectionInfo.Database &&
                item.Attribute("TargetServer")?.Value == targetConnectionInfo.Server &&
                item.Attribute("TargetDatabase")?.Value == targetConnectionInfo.Database
            );

            if (profileElement != null)
            {
                profile = new DataTransferErrorProfile
                {
                    SourceServer = sourceConnectionInfo.Server,
                    SourceDatabase = sourceConnectionInfo.Database,
                    SourceTableName = profileElement.Attribute("SourceTableName")?.Value,

                    TargetServer = targetConnectionInfo.Server,
                    TargetDatabase = targetConnectionInfo.Database,
                    TargetTableName = profileElement.Attribute("TargetTableName")?.Value
                };
            }

            return profile;
        }
    }
}