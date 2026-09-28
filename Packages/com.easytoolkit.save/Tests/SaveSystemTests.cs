using System;
using System.IO;
using Easy.Save.Migrations;
using Easy.Save.Serialization;
using NUnit.Framework;
using UnityEngine;

namespace Easy.Save.Tests
{
    public sealed class SaveSystemTests
    {
        private string testSavePath;

        [SetUp]
        public void SetUp()
        {
            testSavePath = Path.Combine(Path.GetTempPath(), "easy-save-tests", Guid.NewGuid().ToString("N"));
            SaveSystem.Setup();
            SaveSystem.SetBasePath(testSavePath);
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.ResetBasePath();
            if (Directory.Exists(testSavePath))
                Directory.Delete(testSavePath, true);
        }

        [Test]
        public void SaveAndLoad_WritesVersionedPayload()
        {
            SaveSystem.RegisterSaveType(new GenericJsonSerializer<TestSaveData>());

            var original = new TestSaveData { level = 5, displayName = "Saved" };

            SaveSystem.Save("slot/profile", original);
            var loaded = SaveSystem.Load<TestSaveData>("slot/profile");
            var wrapper = JsonUtility.FromJson<VersionedWrapper>(
                File.ReadAllText(SaveSystem.GetPath("slot/profile"))
            );

            Assert.AreEqual("easy.test", wrapper.typeId);
            Assert.AreEqual(2, wrapper.version);
            Assert.AreEqual(original.level, loaded.level);
            Assert.AreEqual(original.displayName, loaded.displayName);
        }

        [Test]
        public void Load_UsesRegisteredMigration()
        {
            var migration = new Migration<TestSaveData>();
            migration.Register(1, json =>
            {
                var old = JsonUtility.FromJson<OldTestSaveData>(json);
                return new TestSaveData { level = old.level, displayName = "Migrated" };
            });
            migration.Register(2, json => JsonUtility.FromJson<TestSaveData>(json));
            SaveSystem.RegisterSaveType(new GenericJsonSerializer<TestSaveData>(), migration);

            var oldPayload = JsonUtility.ToJson(new OldTestSaveData { level = 8 });
            var wrapper = JsonUtility.ToJson(
                new VersionedWrapper
                {
                    typeId = "easy.test",
                    version = 1,
                    payload = oldPayload,
                }
            );

            string savePath = SaveSystem.GetPath("slot/profile");
            Directory.CreateDirectory(Path.GetDirectoryName(savePath));
            File.WriteAllText(savePath, wrapper);

            var loaded = SaveSystem.Load<TestSaveData>("slot/profile");

            Assert.AreEqual(8, loaded.level);
            Assert.AreEqual("Migrated", loaded.displayName);
        }

        [Test]
        public void ValidateKey_RejectsParentSegments()
        {
            Assert.Throws<ArgumentException>(() => SaveSystem.GetPath("../profile"));
        }

        [Serializable]
        private sealed class OldTestSaveData
        {
            public int level;
        }

        [Serializable]
        [SaveType("easy", "test", 2)]
        private sealed class TestSaveData
        {
            public int level;
            public string displayName;
        }
    }
}
