using System.Windows.Input;
using AnnoDesigner.Core.Helper;
using AnnoDesigner.Core.Models;

namespace AnnoDesigner.ViewModels
{
    public class AboutViewModel : Notify
    {
        public AboutViewModel()
        {
            OpenOriginalHomepageCommand = new RelayCommand(OpenOriginalHomepage);
            OpenProjectHomepageCommand = new RelayCommand(OpenProjectHomepage);
            OpenWikiHomepageCommand = new RelayCommand(OpenWikiHomepage);
            CloseWindowCommand = new RelayCommand<ICloseable>(CloseWindow);
        }

        #region commands

        public ICommand OpenOriginalHomepageCommand { get; private set; }

        private void OpenOriginalHomepage(object param)
        {
            ProcessHelper.OpenUrl("http://code.google.com/p/anno-designer/");
        }

        public ICommand OpenProjectHomepageCommand { get; private set; }

        private void OpenProjectHomepage(object param)
        {
            ProcessHelper.OpenUrl("https://github.com/Just-Chaldea/anno-designer/");
        }

        public ICommand OpenWikiHomepageCommand { get; private set; }

        private void OpenWikiHomepage(object param)
        {
            ProcessHelper.OpenUrl("https://anno1800.fandom.com/wiki/Anno_Designer");
        }

        public ICommand CloseWindowCommand { get; private set; }

        private void CloseWindow(ICloseable window)
        {
            window?.Close();
        }

        #endregion
    }
}
