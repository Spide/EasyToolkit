using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NUnit.Framework;
using Easy.State;

namespace Easy.State.Module.Tests
{
    public class StateTests
    {
        private enum WeaponAttribute {
            DAMAGE,
            ACURRACY,
            RANGE,
        }

        [Test]
        public void testBasics()
        {
            var weaponStatistics = new GenericState<WeaponAttribute>();
            weaponStatistics.Set(WeaponAttribute.ACURRACY, 1);
            weaponStatistics.Set(WeaponAttribute.DAMAGE, 1);
            weaponStatistics.Set(WeaponAttribute.RANGE, 1);

            var perks = new GenericState<WeaponAttribute>();
            perks.Set(WeaponAttribute.DAMAGE, 100);

            var penalties = new GenericState<WeaponAttribute>();
            penalties.Set(WeaponAttribute.DAMAGE, -50);

            weaponStatistics.Plus(WeaponAttribute.DAMAGE, 5);
            weaponStatistics.Plus(WeaponAttribute.DAMAGE, -4);

            Assert.True(weaponStatistics.Get(WeaponAttribute.DAMAGE) == 2, "Damage should be 2 now");
            Assert.True(StateUtils.Sum(WeaponAttribute.DAMAGE, weaponStatistics, perks, penalties ) == 52, "Damage should be 2 now");
        }

        [Test]
        public void PlusFloat_MissingKey_StartsFromZeroAndReturnsTotal()
        {
            var state = new GenericState<string>();
            float total = state.Plus("speed", 1.5f);

            Assert.AreEqual(1.5f, total);
            Assert.AreEqual(1.5f, state.Get<float>("speed"));
        }

        [Test]
        public void PlusFloat_AddsToStoredFloat_AndAcceptsNegativeValues()
        {
            var state = new GenericState<string>();
            state.Set("speed", 2.25f);

            Assert.AreEqual(3.0f, state.Plus("speed", 0.75f));
            Assert.AreEqual(2.0f, state.Plus("speed", -1.0f));
            Assert.AreEqual(2.0f, state.Get<float>("speed"));
        }

        [Test]
        public void PlusFloat_StoredInt_IsConvertedAndResultIsStoredAsFloat()
        {
            var state = new GenericState<string>();
            state.Set("damage", 10);

            Assert.AreEqual(10.5f, state.Plus("damage", 0.5f));
            Assert.AreEqual(10.5f, state.Get<float>("damage"));
            Assert.Throws<InvalidCastException>(() => state.Get<int>("damage"));
        }

        [Test]
        public void PlusFloat_NonNumericValue_ThrowsAndLeavesStateUnchanged()
        {
            var state = new GenericState<string>();
            state.Set("name", "axe");
            Assert.Throws<InvalidCastException>(() => state.Plus("name", 1f));

            Assert.AreEqual("axe", state.Get<string>("name"));
        }

        [Test]
        public void PlusFloat_RaisesOnChangeWithNewTotal()
        {
            var state = new GenericState<string>();
            string changedKey = null;
            object changedValue = null;
            state.OnChange += (k, v) => { changedKey = k; changedValue = v; };

            state.Plus("speed", 2f);

            Assert.AreEqual("speed", changedKey);
            Assert.AreEqual(2f, changedValue);
        }

        [Test]
        public void PlusInt_StillBindsToIntOverload()
        {
            var state = new GenericState<string>();
            state.Plus("count", 3);

            Assert.AreEqual(3, state.Get("count"));
        }
    }
}
