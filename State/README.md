# EasyToolkit State
If you need dynamic or extensible structure for your states like statistics 

Example:
```
var weaponStatistics = new GenericState<WeaponAttribute>();
weaponStatistics.Set(WeaponAttribute.ACURRACY, 100);
weaponStatistics.Set(WeaponAttribute.DAMAGE, 100);
weaponStatistics.Set(WeaponAttribute.RANGE, 100);

var perks = new GenericState<WeaponAttribute>();
perks.Set(WeaponAttribute.DAMAGE, 100);
perks.Set(WeaponAttribute.RANGE, 20);

var penalties = new GenericState<WeaponAttribute>();
penalties.Set(WeaponAttribute.DAMAGE, -150);

// total damage will be 50
var totalDamage = StateUtils.Sum(WeaponAttribute.DAMAGE, weaponStatistics, perks, penalties );

```

Look in to tests for more examples of use

