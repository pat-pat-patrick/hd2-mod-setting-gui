using System.Windows;

namespace Hd2ModSettingGui
{
    public partial class App : Application
    {
        public App() => Resources.MergedDictionaries.Add(Theme.Res);
    }
}
