using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NUnit.Framework;
using Easy.BehaviourTree;
using System;
using System.Runtime.InteropServices;
using System.Data;

namespace Easy.BehaviourTree.Tests
{
    public class BehaviourTreeTests
    {

        [Test]
        public void selector_test()
        {
            var blackboard = new TestBlackboard();
            var root = new Selector<TestBlackboard, string>();


            root.addChild((new InvokeNode((data) => { return data.testVal == 1 ? Result.SUCCESS : Result.FAILED; })));
            root.addChild((new InvokeNode((data) => { return data.testVal == 2 ? Result.SUCCESS : Result.FAILED; })));
            root.addChild((new InvokeNode((data) => { return data.testVal == 3 ? Result.SUCCESS : Result.FAILED; })));
            root.Initialize(blackboard);

            Assert.AreEqual(Result.FAILED, root.Run());

            blackboard.testVal = 2;
            Assert.AreEqual(Result.SUCCESS, root.Run());

        }

        [Test]
        public void condition_test()
        {
            var blackboard = new TestBlackboard();
            var root = new Selector<TestBlackboard, string>();


            // reload
            var node1 = (new InvokeNode((data) => { data.testVal = 3; Debug.Log("reload"); return Result.SUCCESS; }));
            var condition1 = new ConditionDecorator<TestBlackboard, string>((data) => { return data.testVal == 0; });
            condition1.Child = node1;

            // shoot until have ammo
            var node2 = (new InvokeNode((data) => { if (data.testVal <= 0) return Result.FAILED; data.testVal--; Debug.Log("shoot"); return Result.SUCCESS; }));
            var condition2 = new ConditionDecorator<TestBlackboard, string>((data) => { return data.testVal > 0; });
            condition2.Child = node2;


            root.addChild(condition1);
            root.addChild(condition2);
            root.Initialize(blackboard);

            root.Run(); // reload
            Assert.AreEqual(3, blackboard.testVal);
            root.Run(); // shoot
            root.Run(); // shoot
            root.Run(); // shoot
            Assert.AreEqual(0, blackboard.testVal);
            root.Run(); // reload
            Assert.AreEqual(3, blackboard.testVal);

        }

        [Test]
        public void sequence_test()
        {
            var blackboard = new TestBlackboard();
            var root = new Sequence<TestBlackboard, string>();
            root.addChild(new PrintNode("first"));
            root.addChild(new InvokeNode((data) => { return data.testVal >= 1 ? Result.SUCCESS : Result.FAILED; }));
            root.addChild(new PrintNode("second"));
            root.addChild(new InvokeNode((data) => { return data.testVal >= 2 ? Result.SUCCESS : Result.FAILED; }));
            root.addChild(new PrintNode("third"));
            root.addChild(new InvokeNode((data) => { return data.testVal >= 3 ? Result.SUCCESS : Result.FAILED; }));

            root.Initialize(blackboard);

            Assert.AreEqual(Result.FAILED, root.Run());
            blackboard.testVal++;
            Assert.AreEqual(Result.FAILED, root.Run());
            blackboard.testVal++;
            Assert.AreEqual(Result.FAILED, root.Run());
            blackboard.testVal++;
            Assert.AreEqual(Result.SUCCESS, root.Run());

        }

        [Test]
        public void testRules()
        {
            TestBlackboard blackboard = new TestBlackboard();
            var test = true;
            var result = 0;
            INode<TestBlackboard, string> root = TestBuilder
            .Selector()
                .AddChild(TestBuilder.Decorate(new InvokeNode((data) => { result++; return Result.SUCCESS; })).Rule((d) => test == true).Build())
                .AddChild(TestBuilder.Decorate(new InvokeNode((data) => { result--; return Result.SUCCESS; })).Rule((d) => test == false).Build())
            .Build();

            root.Initialize(blackboard);

            Debug.LogFormat("Tik 1 {0}", root.Run().ToString());
            Debug.LogFormat("Tik 2 {0}", root.Run().ToString());
            Debug.LogFormat("Tik 3 {0}", root.Run().ToString());

            Assert.AreEqual(3, result);

            test = false;

            root.Run();
            root.Run();
            root.Run();

            Assert.AreEqual(0, result);
        }

        [Test]
        public void testRepeat()
        {
            TestBlackboard blackboard = new TestBlackboard();

            var result = 0;
            INode<TestBlackboard, string> root = TestBuilder
            .Decorate(new InvokeNode((data) => { result++; Debug.LogFormat("added {0}", result); return Result.SUCCESS; }))
                .Repeat(3)
            .Build(blackboard);

            Debug.LogFormat("start with {0}", result);
            root.Run();
            Assert.AreEqual(3, result);

            root.Run();
            Assert.AreEqual(3, result);

        }

        [Test]
        public void testMultiDecorateRepeat()
        {
            TestBlackboard blackboard = new TestBlackboard();

            /// run 10 times but rule stops it on 5  
            var result = 0;
            INode<TestBlackboard, string> root = TestBuilder
                .Decorate(new InvokeNode((data) => { result++; Debug.LogFormat("r {0}", result); return Result.SUCCESS; }))
                .Repeat(10)
                .Rule(d => result < 5)
            .Build(blackboard);

            root.Run();
            Assert.AreEqual(10, result);

            /// run 10 times but rule stops it on 5  
            var result2 = 0;
            INode<TestBlackboard, string> root2 = TestBuilder
                .Decorate(new InvokeNode((data) => { result2++; Debug.LogFormat("r {0}", result2); return Result.SUCCESS; }))
                .Rule(d => result2 < 5)
                .Repeat(10)
            .Build(blackboard);

            root2.Run();
            Assert.AreEqual(5, result2);

        }

        [Test]
        public void testProxy()
        {
            TestBlackboard blackboard = new TestBlackboard();

            /// run 10 times but rule stops it on 5  
            var result = 0;
            INode<TestBlackboard, string> root = TestBuilder
                .Decorate(new InvokeNode((data) => UnityEngine.Random.Range(0, 1) == 0 ? Result.FAILED : Result.SUCCESS))
                .Proxy((child, data, childResult) =>
                {
                    result++;
                    Debug.LogFormat("Proxy pass {0} result {1} ", child.GetType().Name, childResult.ToString());
                    return childResult;
                })
            .Build(blackboard);

            root.Run();
            Assert.AreEqual(1, result);


        }

        [Test]
        public void testBuilderTree()
        {
            
            TestBuilder testBuilder = new TestBuilder();

            var running = TestBuilder.Decorate(TestBuilder.Node<PrintNode>("Second child")).Running().Build();

            INode<TestBlackboard, string> root = TestBuilder
            .Sequence()
                .AddChild(testBuilder.observe(TestBuilder.Node(typeof(PrintNode), "Firtst child")))
                .AddChild(testBuilder.observe(running))
            .Build(testBuilder.Blackboard);

            root.Run();
            
            foreach (var item in testBuilder.State)
            {
                Debug.LogFormat("{0} = {1}", item.Key.GetType().Name, item.Value.ToString());
            }
            
            Assert.AreEqual(Result.RUNNING, testBuilder.State[running]);


        }


        [Test]
        public void testBuilderTreeUtility()
        {
            
            var running = TestBuilder.Decorate(TestBuilder.Node<PrintNode>("Second child")).Running().Build();

            INode<TestBlackboard, string> root = TestBuilder
            .Utility()
                .AddChild(TestBuilder.Node(typeof(PrintNode), "Firtst child"), (data) => 0)
                .AddChild(TestBuilder.Node(typeof(PrintNode), " third"), (data) => 40)
                .AddChild(running, (data) => 100)
            .Build(new TestBlackboard());

        
            
            Assert.AreEqual(Result.RUNNING, root.Run());


        }

        public class TestBuilder : TreeBuilder<TestBlackboard, string>
        {
            TestBlackboard blackboard = new TestBlackboard();
            public TestBuilder() : base()
            {
                Blackboard = blackboard;
            }

                        /// observe state of every node result
            Dictionary<INode<TestBlackboard, string>, Result> state = new Dictionary<INode<TestBlackboard, string>, Result>();

            public Dictionary<INode<TestBlackboard, string>, Result> State { get => state; set => state = value; }
            public TestBlackboard Blackboard { get => blackboard; set => blackboard = value; }

            public DecoratorNode<TestBlackboard, string> observe (INode<TestBlackboard, string> node) { 
                var proxy =  new ProxyDecorator<TestBlackboard, string>((n, data , r) => { State[node] = r; return r; });
                proxy.Child = node;
                return proxy;
            }

        }

        private class PrintNode : Node<TestBlackboard, string>
        {
            public String message;

            public PrintNode(String msg)
            {
                message = msg;
            }

            public PrintNode()
            {
            }


            public override Result Run()
            {
                Debug.Log(message);
                return Result.SUCCESS;
            }
        }

        private class InvokeNode : Node<TestBlackboard, string>
        {
            public Func<TestBlackboard, Result> action;

            public InvokeNode(Func<TestBlackboard, Result> eval)
            {
                this.action = eval;
            }

            public override Result Run()
            {
                return action.Invoke(blackboard);
            }
        }

        public class TestBlackboard : IBlackboard<string>
        {
            public int testVal = 0;

            public T GetVariable<T>(string key)
            {
                throw new NotImplementedException();
            }

            public void SetVariable(string key, object value)
            {
                throw new NotImplementedException();
            }
        }
    }
}
