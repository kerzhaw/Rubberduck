﻿using System.Runtime.InteropServices;
using NLog;
using Rubberduck.Settings;
using Rubberduck.UI.SourceControl;

namespace Rubberduck.UI.Command
{
    /// <summary>
    /// A command that displays the Source Control panel.
    /// </summary>
    [ComVisible(false)]
    public class ShowSourceControlPanelCommand : CommandBase
    {
        private readonly IPresenter _presenter;

        public ShowSourceControlPanelCommand(SourceControlDockablePresenter presenter) : base(LogManager.GetCurrentClassLogger())
        {
            _presenter = presenter;
        }

        protected override void ExecuteImpl(object parameter)
        {
            _presenter.Show();
        }

        public override RubberduckHotkey Hotkey
        {
            get { return RubberduckHotkey.SourceControl; }
        }
    }
}
