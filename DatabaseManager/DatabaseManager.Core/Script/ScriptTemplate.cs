using System.IO;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using DatabaseManager.Model;

namespace DatabaseManager.Core
{
    public class ScriptTemplate
    {
        private const string commonTemplateFileName = "Common";
        private const string commonTemplateFileExtension = ".txt";
        private readonly DbInterpreter dbInterpreter;

        public ScriptTemplate(DbInterpreter dbInterpreter)
        {
            this.dbInterpreter = dbInterpreter;
        }

        public string TemplateFolder => Path.Combine(PathHelper.GetAssemblyFolder(), "Config/Template");

        public string GetTemplateContent(DatabaseObjectType databaseObjectType, ScriptAction scriptAction,
            DatabaseObject databaseObject)
        {
            var scriptTypeName = databaseObjectType.ToString();
            var scriptTypeFolder = Path.Combine(TemplateFolder, scriptTypeName);

            var scriptTemplateFilePath =
                Path.Combine(scriptTypeFolder, dbInterpreter.DatabaseType + commonTemplateFileExtension);

            if (!File.Exists(scriptTemplateFilePath))
                scriptTemplateFilePath =
                    Path.Combine(scriptTypeFolder, commonTemplateFileName + commonTemplateFileExtension);

            if (!File.Exists(scriptTemplateFilePath)) return string.Empty;

            var templateContent = File.ReadAllText(scriptTemplateFilePath);

            templateContent =
                ReplaceTemplatePlaceHolders(templateContent, databaseObjectType, scriptAction, databaseObject);

            return templateContent;
        }

        private string ReplaceTemplatePlaceHolders(string templateContent, DatabaseObjectType databaseObjectType,
            ScriptAction scriptAction, DatabaseObject databaseObject)
        {
            var nameTemplate = $"{databaseObjectType.ToString().ToUpper()}_NAME";

            var name = dbInterpreter.DatabaseType == DatabaseType.SqlServer
                ? dbInterpreter.GetQuotedDbObjectNameWithSchema(databaseObject?.Schema, nameTemplate)
                : dbInterpreter.GetQuotedString(nameTemplate);

            var tableName = databaseObjectType == DatabaseObjectType.Trigger && databaseObject != null
                ? dbInterpreter.GetQuotedDbObjectNameWithSchema(databaseObject)
                : dbInterpreter.GetQuotedString("TABLE_NAME");


            templateContent = templateContent.Replace("$ACTION$", scriptAction.ToString())
                .Replace("$NAME$", name)
                .Replace("$TABLE_NAME$", tableName);

            return templateContent;
        }
    }
}