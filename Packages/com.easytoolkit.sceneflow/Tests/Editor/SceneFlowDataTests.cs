using NUnit.Framework;
using UnityEngine;

namespace Easy.SceneFlow.Tests
{
    public sealed class SceneFlowDataTests
    {
        private LevelDefinition Level(string key, string scene, string category = "Gameplay")
        {
            var level = ScriptableObject.CreateInstance<LevelDefinition>();
            level.Configure(key, scene, category);
            return level;
        }

        private LevelCatalog Catalog(params LevelDefinition[] levels)
        {
            var catalog = ScriptableObject.CreateInstance<LevelCatalog>();
            catalog.Configure(levels);
            return catalog;
        }

        [Test]
        public void Catalog_FindsLevelsByKeyAndScene()
        {
            LevelCatalog catalog = Catalog(Level("menu", "MainMenu", "Menu"), Level("arena", "Arena"));

            Assert.IsTrue(catalog.TryValidate(out _));
            Assert.IsTrue(catalog.TryGetByKey("arena", out LevelDefinition byKey));
            Assert.AreEqual("Arena", byKey.SceneName);
            Assert.IsTrue(catalog.TryGetBySceneName("MainMenu", out LevelDefinition byScene));
            Assert.AreEqual("menu", byScene.LevelKey);
            Assert.IsFalse(catalog.TryGetByKey("missing", out _));
            Assert.IsFalse(catalog.TryGetByKey(null, out _));
        }

        [Test]
        public void Catalog_RejectsDuplicateKeysScenesAndInvalidEntries()
        {
            Assert.IsFalse(Catalog(Level("a", "A"), Level("a", "B")).TryValidate(out string duplicateKey));
            StringAssert.Contains("duplicate level key", duplicateKey);

            Assert.IsFalse(Catalog(Level("a", "A"), Level("b", "A")).TryValidate(out string duplicateScene));
            StringAssert.Contains("duplicate level scene", duplicateScene);

            Assert.IsFalse(Catalog(Level("a", "A"), null).TryValidate(out string missing));
            StringAssert.Contains("missing", missing);

            Assert.IsFalse(Catalog(Level("", "A")).TryValidate(out _));
        }

        [Test]
        public void Profile_RequiresKnownDefaultLevelAndUniqueContexts()
        {
            LevelCatalog catalog = Catalog(Level("menu", "MainMenu"));
            var profile = ScriptableObject.CreateInstance<SceneFlowProfile>();

            profile.Configure(catalog, "menu", "Initialization", "Loading",
                new PersistentSceneEntry("Systems", "SYSTEMS"),
                new PersistentSceneEntry("UI", "UI"));
            Assert.IsTrue(profile.TryValidate(out string validError), validError);

            profile.Configure(catalog, "unknown", "Initialization", "Loading");
            Assert.IsFalse(profile.TryValidate(out _));

            profile.Configure(catalog, "menu", "Initialization", "Loading",
                new PersistentSceneEntry("Systems", "SYSTEMS"),
                new PersistentSceneEntry("Other", "SYSTEMS"));
            Assert.IsFalse(profile.TryValidate(out _));

            profile.Configure(catalog, "menu", "Initialization", "Loading",
                new PersistentSceneEntry("Systems", SceneFlowContextNames.Level));
            Assert.IsFalse(profile.TryValidate(out _), "LEVEL is reserved for the promoted level.");

            profile.Configure(catalog, "menu", "Initialization", "Loading",
                new PersistentSceneEntry("MainMenu", "MENU"));
            Assert.IsFalse(profile.TryValidate(out _), "A scene cannot be both persistent and a level.");

            profile.Configure(null, "menu", "Initialization", "Loading");
            Assert.IsFalse(profile.TryValidate(out _));
        }

        [Test]
        public void DevelopmentOnlyEntries_LoadInEditor()
        {
            Assert.IsTrue(new PersistentSceneEntry("Debug", "DEBUG", developmentOnly: true).ShouldLoad);
            Assert.IsFalse(new PersistentSceneEntry(string.Empty, "DEBUG").ShouldLoad);
        }

        [Test]
        public void LoadRequest_DefaultsEntryPoint()
        {
            var request = new LevelLoadRequest("arena");
            Assert.AreEqual(LevelLoadRequest.DefaultEntryPointId, request.EntryPointId);
            Assert.IsFalse(request.ForceReload);
            Assert.AreEqual("north", new LevelLoadRequest("arena", "north", true).EntryPointId);
        }

        [Test]
        public void LoadResult_AlreadyActiveCountsAsSuccess()
        {
            Assert.IsTrue(new LevelLoadResult(LevelLoadStatus.AlreadyActive, "a").Succeeded);
            Assert.IsFalse(new LevelLoadResult(LevelLoadStatus.Busy, "a").Succeeded);
        }

        [Test]
        public void TransitionGate_RejectsOverlap()
        {
            var gate = new TransitionGate();
            Assert.IsTrue(gate.TryEnter());
            Assert.IsTrue(gate.IsHeld);
            Assert.IsFalse(gate.TryEnter());
            gate.Exit();
            Assert.IsFalse(gate.IsHeld);
            Assert.IsTrue(gate.TryEnter());
        }

        [Test]
        public void LoadingProgress_ReportsNormalizedStateAndCompletes()
        {
            var progress = new LoadingProgress();
            int notifications = 0;
            progress.Changed += _ => notifications++;

            progress.Report("Systems", 1, 4);
            Assert.AreEqual(0.25f, progress.Current.Normalized, 0.0001f);
            Assert.IsFalse(progress.Current.IsComplete);

            progress.Complete();
            Assert.AreEqual(1f, progress.Current.Normalized, 0.0001f);
            Assert.IsTrue(progress.Current.IsComplete);
            Assert.AreEqual(2, notifications);
        }
    }
}
