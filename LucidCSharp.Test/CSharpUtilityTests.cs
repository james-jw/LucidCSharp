using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LucidCSharp.Tests
{
    [TestClass()]
    public class CSharpUtilityTests
    {
        [TestMethod()]
        public void CompileLambdaWithTwoParameterSupportTest()
        {
            var supportFunctions = new Dictionary<string, string>() {
                { "multiply", "(a, b) => a * b" },
                { "add", "(a, b) => a + b" }
            };

            var function = CSharpUtility.CompileLambda<dynamic, dynamic>("(v) => multiply(v, 5)", supportFunctions);
            Assert.AreEqual(15, function(3));

            function = CSharpUtility.CompileLambda<dynamic, dynamic>("(a) => add(a, \"B\")", supportFunctions);
            Assert.AreEqual("AB", function("A"));

        }

        [TestMethod()]
        public void CompileLambdaWithSupportTest()
        {
            var supportFunctions = new Dictionary<string, string>() {
                { "multiplyBy2", "i => i * 2" },
                { "multiplyBy3", "i => i * 3" }
            };

            var function = CSharpUtility.CompileLambda<dynamic, dynamic>("(source) => multiplyBy3(multiplyBy2(source))", supportFunctions);

            Assert.AreEqual(12, function(2));
        }
        [TestMethod()]
        public void CompileLambdaWithTraceTest()
        {

            dynamic value = null;
            var function = CSharpUtility.CompileLambda<dynamic, dynamic>("(source) => Trace(source) * 2", null, (v, m) => {
                value = v;
                return v;
            });

            Assert.AreEqual(4, function(2));
            Assert.AreEqual(2, value);

            function = CSharpUtility.CompileLambda<dynamic, dynamic>("(source) => Trace(source, \"Message\") * 2", null, (v, m) => {
                value = m;
                return v;
            });

            function(2);
            Assert.AreEqual("Message", value);
        }

        [TestMethod()]
        public void CompileLambdaTest()
        {
            var function = CSharpUtility.CompileLambda<dynamic, dynamic>("(source) => source * 2");

            Assert.AreEqual(4, function(2));
        }

        [TestMethod()]
        public void CompileTwoParameterLambdaTest()
        {
            var function = CSharpUtility.CompileLambda<dynamic, dynamic, dynamic>("(arg1, arg2) => arg1 * arg2");

            Assert.AreEqual(4, function(2, 2));
        }

        [TestMethod()]
        public void LambdaParameterCountTest()
        {
            var singleLambda = "(source) => false";
            var singleLambdaVariant = "source => false";

            Assert.AreEqual(1, CSharpUtility.LambdaParameterCount(singleLambda));
            Assert.AreEqual(1, CSharpUtility.LambdaParameterCount(singleLambdaVariant));

            var doubleLambda = "(device, otherDevice) => false";
            var doubleLambdaVariant = "(device, otherDevice) => false";

            Assert.AreEqual(2, CSharpUtility.LambdaParameterCount(doubleLambda));
            Assert.AreEqual(2, CSharpUtility.LambdaParameterCount(doubleLambdaVariant));

            var zeroLambda = "() => false";

            Assert.AreEqual(0, CSharpUtility.LambdaParameterCount(zeroLambda));
        }

        [TestMethod()]
        public void FormatCodeTest()
        {
            string expected = @"(source) =>
{
    string ohug = ohugOrientation(source);
    string phase_count_string = phasingCountString123(source.PhasingCode);
    return ohug + "" "" + phase_count_string;
}";

            string input = @"(source) => { string ohug = ohugOrientation(source); string phase_count_string = phasingCountString123(source.PhasingCode); return
                ohug + "" "" + phase_count_string; }";

            var output = CSharpUtility.FormatLambdaString(input);

            Assert.AreEqual(expected, output);
        }
    }
}