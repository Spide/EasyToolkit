using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using static Easy.Utils.RegexUtils;

namespace Easy.Utils.Module.Tests
{
    public class Tests
    {
        [Test]
        public void MatchingTest()
        {
            var matcher = WildcardMatcher("t*");

            Assert.IsTrue(matcher.IsMatch("t"), "matching with t");
            Assert.IsTrue(matcher.IsMatch("te"), "matching with te");
            Assert.IsTrue(matcher.IsMatch("test"), "matching with test");
            Assert.IsFalse(matcher.IsMatch("Not"), "should not match");

            var matcher2 = WildcardMatcher("test*test");

            Assert.IsTrue(matcher2.IsMatch("testtest"), "matching with testtest");
            Assert.IsTrue(matcher2.IsMatch("testtesttesttest"), "matching with testtesttesttest");
            Assert.IsFalse(matcher2.IsMatch("test"), "matching with test");



        }


    }


}
