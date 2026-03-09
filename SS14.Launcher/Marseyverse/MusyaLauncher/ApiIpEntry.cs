using ReactiveUI;

namespace SS14.Launcher.ViewModels.MainWindowTabs
{
    public class ApiIpEntry : ReactiveObject
    {
        private string _ip;
        public string Ip
        {
            get => _ip;
            set => this.RaiseAndSetIfChanged(ref _ip, value);
        }

        public ApiIpEntry(string ip = "") => _ip = ip;
        public override string ToString() => Ip;
    }
}
