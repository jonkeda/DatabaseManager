using System;

namespace Databases.Model.Script
{
    public class NewLineSript : Script
    {
        public NewLineSript()
        {
            Content = Environment.NewLine;
        }
    }
}