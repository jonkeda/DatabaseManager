namespace Databases.Manager.Model.DbObjectDisplay
{
    public interface IDbObjContentDisplayer
    {
        void Show(DatabaseObjectDisplayInfo displayInfo);
        ContentSaveResult Save(ContentSaveInfo saveInfo);
    }
}