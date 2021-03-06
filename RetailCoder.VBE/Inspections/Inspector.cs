﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Rubberduck.Parsing.VBA;
using Rubberduck.Settings;
using Rubberduck.UI;
using Antlr4.Runtime.Tree;
using Rubberduck.Parsing;
using Rubberduck.Parsing.Grammar;

namespace Rubberduck.Inspections
{
    namespace Rubberduck.Inspections
    {
        public class Inspector : IInspector
        {
            private readonly IGeneralConfigService _configService;
            private readonly List<IInspection> _inspections;

            public Inspector(IGeneralConfigService configService, IEnumerable<IInspection> inspections)
            {
                _inspections = inspections.ToList();

                _configService = configService;
                configService.SettingsChanged += ConfigServiceSettingsChanged;
            }

            private void ConfigServiceSettingsChanged(object sender, EventArgs e)
            {
                UpdateInspectionSeverity();
            }

            private void UpdateInspectionSeverity()
            {
                var config = _configService.LoadConfiguration();

                foreach (var inspection in _inspections)
                {
                    foreach (var setting in config.UserSettings.CodeInspectionSettings.CodeInspections)
                    {
                        if (inspection.Description == setting.Description)
                        {
                            inspection.Severity = setting.Severity;
                        }
                    }
                }
            }

            public async Task<IEnumerable<ICodeInspectionResult>> FindIssuesAsync(RubberduckParserState state, CancellationToken token)
            {
                if (state == null || !state.AllUserDeclarations.Any())
                {
                    return new ICodeInspectionResult[] { };
                }

                state.OnStatusMessageUpdate(RubberduckUI.CodeInspections_Inspecting);
                UpdateInspectionSeverity();

                var allIssues = new ConcurrentBag<ICodeInspectionResult>();

                // Prepare ParseTreeWalker based inspections
                var parseTreeWalkResults = GetParseTreeResults(state);
                foreach (var parseTreeInspection in _inspections.Where(inspection => inspection.Severity != CodeInspectionSeverity.DoNotShow && inspection is IParseTreeInspection))
                {
                    (parseTreeInspection as IParseTreeInspection).ParseTreeResults = parseTreeWalkResults;
                }

                var inspections = _inspections.Where(inspection => inspection.Severity != CodeInspectionSeverity.DoNotShow)
                    .Select(inspection =>
                        Task.Run(() =>
                        {
                            token.ThrowIfCancellationRequested();
                            var inspectionResults = inspection.GetInspectionResults();
                            
                            foreach (var inspectionResult in inspectionResults)
                            {
                                allIssues.Add(inspectionResult);
                            }
                        }, token)).ToList();

                await Task.WhenAll(inspections);
                state.OnStatusMessageUpdate(RubberduckUI.ResourceManager.GetString("ParserState_" + state.Status, UI.Settings.Settings.Culture)); // should be "Ready"
                return allIssues;
            }

            private ParseTreeResults GetParseTreeResults(RubberduckParserState state)
            {
                var result = new ParseTreeResults();

                foreach (var componentTreePair in state.ParseTrees)
                {
                    /*
                    Need to reinitialize these for each and every ParseTree we process, since the results are aggregated in the instances themselves 
                    before moving them into the ParseTreeResults after qualifying them 
                    */
                    var obsoleteCallStatementListener = new ObsoleteCallStatementInspection.ObsoleteCallStatementListener();
                    var obsoleteLetStatementListener = new ObsoleteLetStatementInspection.ObsoleteLetStatementListener();
                    var emptyStringLiteralListener = new EmptyStringLiteralInspection.EmptyStringLiteralListener();
                    var argListWithOneByRefParamListener = new ProcedureCanBeWrittenAsFunctionInspection.ArgListWithOneByRefParamListener();
                    var malformedAnnotationListenter = new MalformedAnnotationInspection.MalformedAnnotationStatementListener();

                    var combinedListener = new CombinedParseTreeListener(new IParseTreeListener[]{
                        obsoleteCallStatementListener,
                        obsoleteLetStatementListener,
                        emptyStringLiteralListener,
                        argListWithOneByRefParamListener,
                        malformedAnnotationListenter
                    });

                    ParseTreeWalker.Default.Walk(combinedListener, componentTreePair.Value);

                    result.ArgListsWithOneByRefParam = result.ArgListsWithOneByRefParam.Concat(argListWithOneByRefParamListener.Contexts.Select(context => new QualifiedContext(componentTreePair.Key, context)));
                    result.EmptyStringLiterals = result.EmptyStringLiterals.Concat(emptyStringLiteralListener.Contexts.Select(context => new QualifiedContext(componentTreePair.Key, context)));
                    result.ObsoleteLetContexts = result.ObsoleteLetContexts.Concat(obsoleteLetStatementListener.Contexts.Select(context => new QualifiedContext(componentTreePair.Key, context)));
                    result.ObsoleteCallContexts = result.ObsoleteCallContexts.Concat(obsoleteCallStatementListener.Contexts.Select(context => new QualifiedContext(componentTreePair.Key, context)));
                    result.MalformedAnnotations = result.MalformedAnnotations.Concat(malformedAnnotationListenter.Contexts.Select(context => new QualifiedContext<VBAParser.AnnotationContext>(componentTreePair.Key, context)));
                }
                return result;
            }

            public void Dispose()
            {
                if (_configService != null)
                {
                    _configService.SettingsChanged -= ConfigServiceSettingsChanged;
                }

                _inspections.Clear();
            }
        }
    }
}
