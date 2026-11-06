using System;
using System.Windows;
using System.Windows.Media.Animation;
using SwissTechKnife.Sdk;

namespace PadTide.Module
{
    public partial class ModulePage
    {
        private readonly HostedAppSupervisor _supervisor;
        private readonly IModuleContext _context;
        private HostedAppState? _shown;

        internal ModulePage(HostedAppSupervisor supervisor, IModuleContext context)
        {
            _supervisor = supervisor;
            _context = context;
            InitializeComponent();
            _supervisor.StateChanged += OnStateChanged;
            Apply(_supervisor.State);
        }

        private void OnStateChanged(HostedAppState state) =>
            _context.Dispatcher.BeginInvoke(new Action(() => Apply(_supervisor.State)));

        private void Apply(HostedAppState state)
        {
            if (_shown == state) return;
            var first = _shown == null;
            _shown = state;

            string label, detail, soft, strong;
            switch (state)
            {
                case HostedAppState.Running:
                    label = "Em execução";
                    detail = "O PadTide está acompanhando a bateria do controle.";
                    soft = "Brush.Success.Soft";
                    strong = "Brush.Success";
                    break;
                case HostedAppState.Starting:
                    label = "Iniciando";
                    detail = "O PadTide está sendo iniciado.";
                    soft = "Brush.Info.Soft";
                    strong = "Brush.Info";
                    break;
                case HostedAppState.RunningOutside:
                    label = "Aberto fora do SwissTechKnife";
                    detail = "Outro PadTide já está aberto e continua mostrando a bateria. O módulo assume assim que ele for fechado.";
                    soft = "Brush.Warning.Soft";
                    strong = "Brush.Warning";
                    break;
                case HostedAppState.Failed:
                    label = "Parado";
                    detail = "O PadTide fechou inesperadamente várias vezes. Abra de novo para tentar.";
                    soft = "Brush.Danger.Soft";
                    strong = "Brush.Danger";
                    break;
                default:
                    label = "Parado";
                    detail = "O PadTide não está em execução. Abra para iniciá-lo.";
                    soft = "Brush.Surface4";
                    strong = "Brush.Text.Secondary";
                    break;
            }

            StatusText.Text = label;
            StatusDetail.Text = detail;
            StatusChip.SetResourceReference(BackgroundProperty, soft);
            StatusText.SetResourceReference(ForegroundProperty, strong);

            if (!first) FadeIn(StatusChip, StatusDetail);
        }

        private void FadeIn(params UIElement[] elements)
        {
            var duration = TryFindResource("Motion.Normal") is Duration configured ? configured : new Duration(TimeSpan.FromMilliseconds(200));
            foreach (var element in elements)
            {
                var fade = new DoubleAnimation(0.4, 1, duration) { EasingFunction = TryFindResource("Ease.Out") as IEasingFunction };
                fade.Freeze();
                element.BeginAnimation(OpacityProperty, fade);
            }
        }

        private void OpenButton_OnClick(object sender, RoutedEventArgs e) => _supervisor.OpenSettings();
    }
}
