using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Inedo.ExecutionEngine;
using Inedo.ExecutionEngine.Executer;
using Inedo.ExecutionEngine.Parser;
using Inedo.Extensibility.RaftRepositories;
using Inedo.IO;
using Inedo.Romp.Configuration;
using Inedo.Romp.Data;
using Inedo.Serialization;

namespace Inedo.Romp
{
    internal static class RompRaftFactory
    {
        private static readonly Dictionary<string, Tables.Rafts> rafts = new Dictionary<string, Tables.Rafts>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, NamedTemplate> templates = new ConcurrentDictionary<string, NamedTemplate>(StringComparer.OrdinalIgnoreCase);

        public static void Initialize()
        {
            int id = 1;
            foreach (var item in RompConfig.Rafts)
            {
                rafts.Add(
                    item.Key,
                    new Tables.Rafts
                    {
                        Raft_Id = id++,
                        Raft_Name = item.Key,
                        Raft_Configuration = getConfig(item.Value)
                    }
                );
            }

            string getConfig(string path)
            {
                if (FileEx.Exists(path) && path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    using (var zipRaft = new ZipRaftRepository { FileName = path })
                    {
                        return Persistence.SerializeToPersistedObjectXml(zipRaft);
                    }
                }
                else
                {
                    using (var fsRaft = new DirectoryRaftRepository { ReadOnly = true, RepositoryPath = path })
                    {
                        return Persistence.SerializeToPersistedObjectXml(fsRaft);
                    }
                }
            }
        }
        public static RaftRepository GetRaft(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));

            var data = rafts.GetValueOrDefault(name);
            if (data != null)
                return (RaftRepository)Persistence.DeserializeFromPersistedObjectXml(data.Raft_Configuration);

            return null;
        }
        public static NamedTemplate GetTemplate(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));

            return templates.GetOrAdd(name, n => LoadTemplateAsync(n).GetAwaiter().GetResult());
        }
        public static DbDataReader Rafts_GetRafts()
        {
            var table = new DataTable(nameof(Tables.Rafts))
            {
                Columns =
                {
                    { nameof(Tables.Rafts.Raft_Id), typeof(int) },
                    { nameof(Tables.Rafts.Raft_Name), typeof(string) },
                    { nameof(Tables.Rafts.Environment_Id), typeof(int) },
                    { nameof(Tables.Rafts.Raft_Configuration), typeof(string) }
                }
            };

            foreach (var raft in rafts.Values.OrderBy(r => r.Raft_Id))
                table.Rows.Add(raft.Raft_Id, raft.Raft_Name, raft.Environment_Id, raft.Raft_Configuration);

            return table.CreateDataReader();
        }

        private static async Task<NamedTemplate> LoadTemplateAsync(string name)
        {
            var qualifiedName = QualifiedName.Parse(name);
            using (var raft = GetRaft(qualifiedName.Namespace ?? RaftRepository.DefaultName))
            {
                if (raft != null)
                {
                    var template = await raft.GetRaftItemAsync(RaftItemType.Module, qualifiedName.Name).ConfigureAwait(false);
                    if (template != null)
                    {
                        using (var stream = await raft.OpenRaftItemAsync(RaftItemType.Module, template.ItemName, FileMode.Open, FileAccess.Read).ConfigureAwait(false))
                        {
                            var results = Compiler.Compile(stream);
                            if (results.Script != null)
                                return results.Script.Templates.Values.FirstOrDefault();
                            else
                                throw new ExecutionFailureException($"Error processing template {name}: {string.Join(Environment.NewLine, results.Errors)}");
                        }
                    }
                }
            }

            return null;
        }
    }
}
