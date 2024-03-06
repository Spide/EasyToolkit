using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NUnit.Framework;
using Easy.BehaviourTree;
using System;
using System.Runtime.InteropServices;

namespace Easy.BehaviourTree.Tests
{
    public class BehaviourTreeTests
    {

        [Test]
        public void selector_test()
        {
            var blackboard = new TestBlackboard();
            var root = new Selector<TestBlackboard>();
            root.Initialize(blackboard);

            root.addChild((new InvokeNode((data) => { return data.testVal == 1 ? Result.SUCCESS : Result.FAILED; } )).Initialize(blackboard));
            root.addChild((new InvokeNode((data) => { return data.testVal == 2 ? Result.SUCCESS : Result.FAILED; } )).Initialize(blackboard));
            root.addChild((new InvokeNode((data) => { return data.testVal == 3 ? Result.SUCCESS : Result.FAILED; } )).Initialize(blackboard));

            Assert.AreEqual(Result.FAILED, root.Run());

            blackboard.testVal = 2;
            Assert.AreEqual(Result.SUCCESS, root.Run());

        }

        [Test]
        public void condition_test()
        {
            var blackboard = new TestBlackboard();
            var root = new Selector<TestBlackboard>();
            root.Initialize(blackboard);

            // reload
            var node1 = (new InvokeNode((data) => { data.testVal = 3; Debug.Log("reload"); return Result.SUCCESS; } )).Initialize(blackboard);
            var condition1 = (new ConditionDecorator<TestBlackboard>()).Initialize(blackboard, node1,
                (data) => { return data.testVal == 0; }
            );

            // shoot until have ammo
            var node2 = (new InvokeNode((data) => {  if (data.testVal <= 0) return Result.FAILED; data.testVal--; Debug.Log("shoot");  return Result.SUCCESS; } )).Initialize(blackboard);
            var condition2 = (new ConditionDecorator<TestBlackboard>()).Initialize(blackboard, node2,
                (data) => { return data.testVal > 0; }
            );

            root.addChild(condition1);
            root.addChild(condition2);

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
            var root = new Sequence<TestBlackboard>();
            root.Initialize(blackboard);

            root.addChild((new PrintNode("first")).Initialize(blackboard));
            root.addChild((new InvokeNode((data) => { return data.testVal >= 1 ? Result.SUCCESS : Result.FAILED; } )).Initialize(blackboard));
            root.addChild((new PrintNode("second")).Initialize(blackboard));
            root.addChild((new InvokeNode((data) => { return data.testVal >= 2 ? Result.SUCCESS : Result.FAILED; } )).Initialize(blackboard));
            root.addChild((new PrintNode("third")).Initialize(blackboard));
            root.addChild((new InvokeNode((data) => { return data.testVal >= 3 ? Result.SUCCESS : Result.FAILED; } )).Initialize(blackboard));

            Assert.AreEqual(Result.FAILED, root.Run());
            blackboard.testVal++;
            Assert.AreEqual(Result.FAILED, root.Run());
            blackboard.testVal++;
            Assert.AreEqual(Result.FAILED, root.Run());
            blackboard.testVal++;
            Assert.AreEqual(Result.SUCCESS, root.Run());

        }

        private class PrintNode : Node<TestBlackboard>
        {
            public String message;

            public PrintNode (String msg){
                message = msg;
            }

            public override Result Run(){
                Debug.Log(message);
                return Result.SUCCESS;
            }
        }

        private class InvokeNode : Node<TestBlackboard>
        {
            public Func<TestBlackboard, Result> action;

            public InvokeNode (Func<TestBlackboard, Result> eval){
                this.action = eval;
            }

            public override Result Run(){
                return action.Invoke(blackboard);
            }
        }

        public class TestBlackboard : IBlackboard
        {
            public int testVal = 0;
        }
    }
}
