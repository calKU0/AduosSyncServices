using System;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace AduosSyncServices.ServicesManager.Services
{
    public class LogRefreshService
    {
        private DispatcherTimer? _timer;
        private Func<Task>? _onTick;

        public void Start(TimeSpan interval, Func<Task> onTick)
        {
            Stop();
            _onTick = onTick;
            _timer = new DispatcherTimer { Interval = interval };
            _timer.Tick += HandleTick;
            _timer.Start();
        }

        public void Stop()
        {
            if (_timer == null)
                return;

            _timer.Tick -= HandleTick;
            _timer.Stop();
            _timer = null;
            _onTick = null;
        }

        private async void HandleTick(object? sender, EventArgs e)
        {
            if (_onTick != null)
                await _onTick();
        }
    }
}
