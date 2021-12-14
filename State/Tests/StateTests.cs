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
    }
}
