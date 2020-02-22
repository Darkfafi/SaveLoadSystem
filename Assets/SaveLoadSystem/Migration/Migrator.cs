using RDP.SaveLoadSystem.Internal;
using System.Collections.Generic;
using UnityEngine;

namespace RDP.SaveLoadSystem
{
	public class Migrator
	{
		public const string MIGRATOR_INDEX_KEY = "RESERVED_MIGRATOR_INDEX_KEY_RESERVED";

		public static void Do(Storage storage, Migration[] migrations)
		{
			foreach(var pair in GetMigrationsPerCapsule(migrations))
			{
				List<Migration> capsuleMigrations = pair.Value;
				if (storage.TryRead(pair.Key, out ReadStorageResult storageResult))
				{
					SaveableValueSection migratorLevelSection = storageResult.CapsuleStorage.GetValueSection(MIGRATOR_INDEX_KEY);
					int migratorLevelIndex = migratorLevelSection.IsValid ? (int)migratorLevelSection.GetValue() : 0;
					int preMigrationLevel = migratorLevelIndex;
					for (int i = migratorLevelIndex; i < capsuleMigrations.Count; i++)
					{
						Migration migration = capsuleMigrations[i];
						migration.Do(storageResult);
						// Migrator Level is the one coming after this one, may it be the next loop or in the future
						migratorLevelIndex = i + 1;
					}
					if (preMigrationLevel != migratorLevelIndex)
					{
						storageResult.CapsuleStorage.SetValue(MIGRATOR_INDEX_KEY, migratorLevelIndex);
						storage.FlushRefresh(pair.Key);
					}
				}
			}
		}

		public static void Undo(Storage storage, Migration[] migrations)
		{
			foreach (var pair in GetMigrationsPerCapsule(migrations))
			{
				List<Migration> capsuleMigrations = pair.Value;
				if (storage.TryRead(pair.Key, out ReadStorageResult storageResult))
				{
					SaveableValueSection migratorLevelSection = storageResult.CapsuleStorage.GetValueSection(MIGRATOR_INDEX_KEY);
					int migratorLevelIndex = migratorLevelSection.IsValid ? (int)migratorLevelSection.GetValue() : 0;
					int preMigrationLevel = migratorLevelIndex;
					for (int i = migratorLevelIndex - 1; i >= 0; i--)
					{
						Migration migration = capsuleMigrations[i];
						migration.Undo(storageResult);
						// Migrator Level is set to target the one last undone
						migratorLevelIndex = i;
					}
					if (preMigrationLevel != migratorLevelIndex)
					{
						storageResult.CapsuleStorage.SetValue(MIGRATOR_INDEX_KEY, migratorLevelIndex);
						storage.FlushRefresh(pair.Key);
					}
				}
			}
		}

		private static Dictionary<string, List<Migration>> GetMigrationsPerCapsule(Migration[] migrations)
		{
			Dictionary<string, List<Migration>> migrationsPerCapsule = new Dictionary<string, List<Migration>>();
			for (int i = 0; i < migrations.Length; i++)
			{
				Migration migration = migrations[i];
				if (!migrationsPerCapsule.TryGetValue(migration.CapsuleIDTarget, out List<Migration> capsuleMigrations))
				{
					capsuleMigrations = new List<Migration>();
					migrationsPerCapsule[migration.CapsuleIDTarget] = capsuleMigrations;
				}
				capsuleMigrations.Add(migration);
			}
			return migrationsPerCapsule;
		}
	}
}