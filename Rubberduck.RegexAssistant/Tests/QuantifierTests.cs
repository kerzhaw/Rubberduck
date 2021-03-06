﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rubberduck.RegexAssistant;

namespace RegexAssistantTests
{
    [TestClass]
    public class QuantifierTests
    {
        [TestMethod]
        public void AsteriskQuantifier()
        {
            Quantifier cut = new Quantifier("*");
            Assert.AreEqual(int.MaxValue, cut.MaximumMatches);
            Assert.AreEqual(0, cut.MinimumMatches);
            Assert.AreEqual(QuantifierKind.Wildcard, cut.Kind);
        }

        [TestMethod]
        public void QuestionMarkQuantifier()
        {
            Quantifier cut = new Quantifier("?");
            Assert.AreEqual(1, cut.MaximumMatches);
            Assert.AreEqual(0, cut.MinimumMatches);
            Assert.AreEqual(QuantifierKind.Wildcard, cut.Kind);
        }

        [TestMethod]
        public void PlusQuantifier()
        {
            Quantifier cut = new Quantifier("+");
            Assert.AreEqual(int.MaxValue, cut.MaximumMatches);
            Assert.AreEqual(1, cut.MinimumMatches);
            Assert.AreEqual(QuantifierKind.Wildcard, cut.Kind);
        }

        [TestMethod]
        public void ExactQuantifier()
        {
            Quantifier cut = new Quantifier("{5}");
            Assert.AreEqual(5, cut.MaximumMatches);
            Assert.AreEqual(5, cut.MinimumMatches);
            Assert.AreEqual(QuantifierKind.Expression, cut.Kind);
        }

        [TestMethod]
        public void FullRangeQuantifier()
        {
            Quantifier cut = new Quantifier("{2,5}");
            Assert.AreEqual(2, cut.MinimumMatches);
            Assert.AreEqual(5, cut.MaximumMatches);
            Assert.AreEqual(QuantifierKind.Expression, cut.Kind);
        }

        [TestMethod]
        public void OpenRangeQuantifier()
        {
            Quantifier cut = new Quantifier("{3,}");
            Assert.AreEqual(3, cut.MinimumMatches);
            Assert.AreEqual(int.MaxValue, cut.MaximumMatches);
            Assert.AreEqual(QuantifierKind.Expression, cut.Kind);

        }
    }
}
