﻿using Microsoft.Office.Core;
using Microsoft.Vbe.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rubberduck.VBA.Parser;

namespace Rubberduck.UnitTesting.UI
{
    internal class RefactorMenu : IDisposable
    {
        private readonly VBE _vbe;
        private readonly Parser _parser;

        public RefactorMenu(VBE vbe)
        {
            _vbe = vbe;
            _parser = new Parser();
        }

        private CommandBarButton _parseModuleButton;
        public CommandBarButton ParseModuleButton { get { return _parseModuleButton; } }

        public void Initialize(CommandBarControls menuControls)
        {
            var menu = menuControls.Add(Type: MsoControlType.msoControlPopup, Temporary: true) as CommandBarPopup;
            menu.Caption = "&Refactor";

            _parseModuleButton = AddMenuButton(menu);
            _parseModuleButton.Caption = "&Parse module";
            _parseModuleButton.FaceId = 3039;
            _parseModuleButton.Click += OnParseModuleButtonClick;

        }

        private void OnParseModuleButtonClick(CommandBarButton Ctrl, ref bool CancelDefault)
        {
            var project = _vbe.ActiveVBProject;
            var component = _vbe.SelectedVBComponent;

            try
            {
                var module = component.CodeModule;
                if (module.CountOfLines < 1)
                {
                    return;
                }

                var code = module.Lines[1, module.CountOfLines];
                var tree = _parser.Parse(project.Name, component.Name, code);
            }
            catch(Exception exception)
            {

            }
        }

        private CommandBarButton AddMenuButton(CommandBarPopup menu)
        {
            return menu.Controls.Add(Type: MsoControlType.msoControlButton) as CommandBarButton;
        }

        public void Dispose()
        {
            
        }
    }
}
