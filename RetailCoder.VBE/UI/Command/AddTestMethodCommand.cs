using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Vbe.Interop;
using NLog;
using Rubberduck.Parsing.Annotations;
using Rubberduck.Parsing.Symbols;
using Rubberduck.Parsing.VBA;
using Rubberduck.UnitTesting;

namespace Rubberduck.UI.Command
{
    /// <summary>
    /// A command that adds a new test method stub to the active code pane.
    /// </summary>
    [ComVisible(false)]
    public class AddTestMethodCommand : CommandBase
    {
        private readonly VBE _vbe;
        private readonly RubberduckParserState _state;

        public AddTestMethodCommand(VBE vbe, RubberduckParserState state) : base(LogManager.GetCurrentClassLogger())
        {
            _vbe = vbe;
            _state = state;
        }

        public const string NamePlaceholder = "%METHODNAME%";
        private const string TestMethodBaseName = "TestMethod";

        public static readonly string TestMethodTemplate = string.Concat(
            "'@TestMethod\r\n",
            "Public Sub ", NamePlaceholder, "() 'TODO ", RubberduckUI.UnitTest_NewMethod_Rename, "\r\n",
            "    On Error GoTo TestFail\r\n",
            "    \r\n",
            "    'Arrange:\r\n\r\n",
            "    'Act:\r\n\r\n",
            "    'Assert:\r\n",
            "    Assert.Inconclusive\r\n\r\n",
            "TestExit:\r\n",
            "    Exit Sub\r\n",
            "TestFail:\r\n",
            "    Assert.Fail \"", RubberduckUI.UnitTest_NewMethod_RaisedTestError, ": #\" & Err.Number & \" - \" & Err.Description\r\n",
            "End Sub\r\n"
            );

        protected override bool CanExecuteImpl(object parameter)
        {
            if (_state.Status != ParserState.Ready || _vbe.ActiveCodePane == null) { return false; }

            var testModules = _state.AllUserDeclarations.Where(d =>
                        d.DeclarationType == DeclarationType.ProceduralModule &&
                        d.Annotations.Any(a => a.AnnotationType == AnnotationType.TestModule));

            try
            {
                // the code modules consistently match correctly, but the components don't
                return testModules.Any(a =>
                            a.QualifiedName.QualifiedModuleName.Component.CodeModule ==
                            _vbe.SelectedVBComponent.CodeModule);
            }
            catch (COMException)
            {
                return false;
            }
        }

        protected override void ExecuteImpl(object parameter)
        {
            if (_vbe.ActiveCodePane == null) { return; }

            try
            {
                var declaration = _state.GetTestModules().FirstOrDefault(f =>
                            f.QualifiedName.QualifiedModuleName.Component.CodeModule == _vbe.ActiveCodePane.CodeModule);

                if (declaration != null)
                {
                    var module = _vbe.ActiveCodePane.CodeModule;
                    var name = GetNextTestMethodName(module.Parent);
                    var body = TestMethodTemplate.Replace(NamePlaceholder, name);
                    module.InsertLines(module.CountOfLines, body);
                }
            }
            catch (COMException)
            {
            }

            _state.OnParseRequested(this, _vbe.SelectedVBComponent);
        }

        private string GetNextTestMethodName(VBComponent component)
        {
            var names = component.GetTests(_vbe, _state).Select(test => test.Declaration.IdentifierName);
            var index = names.Count(n => n.StartsWith(TestMethodBaseName)) + 1;

            return string.Concat(TestMethodBaseName, index);
        }
    }
}
