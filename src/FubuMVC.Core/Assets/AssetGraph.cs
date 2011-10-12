using System;
using System.Collections.Generic;
using System.Linq;
using Bottles.Diagnostics;
using FubuCore.Util;
using FubuMVC.Core.Assets.Combination;
using FubuMVC.Core.Assets.Files;
using FubuCore;

namespace FubuMVC.Core.Assets
{
    public class AssetGraph : IComparer<IFileDependency>, IAssetRegistration
    {
        private readonly Cache<string, IRequestedAsset> _objects = new Cache<string, IRequestedAsset>();
        private readonly Cache<string, AssetSet> _sets = new Cache<string, AssetSet>();
        private readonly List<Extension> _extenders = new List<Extension>();
        private readonly List<DependencyRule> _rules = new List<DependencyRule>();
        private readonly List<PreceedingAsset> _preceedings = new List<PreceedingAsset>();
        private readonly IList<Type> _policyTypes = new List<Type>();
        private readonly Cache<string, IList<string>> _combos = new Cache<string, IList<string>>(key => new List<string>());

        /// <summary>
        /// Use this method in automated tests when you need to set up an
        /// AssetGraph
        /// </summary>
        /// <param name="configure"></param>
        /// <returns></returns>
        public static AssetGraph Build(Action<AssetGraph> configure)
        {
            var graph = new AssetGraph();
            configure(graph);
            graph.CompileDependencies(new PackageLog());

            return graph;
        }

        public AssetGraph()
        {
            _sets.OnMissing = name => new AssetSet{
                Name = name
            };

            _sets.OnAddition = (@set) =>
            {
                _objects[@set.Name] = @set;
            };


            _objects.OnMissing = name =>
            {
                return 
                    _objects.GetAll().FirstOrDefault(x => x.Matches(name)) 
                    ?? 
                    new FileDependency(name);
            };
        }

        public int Compare(IFileDependency x, IFileDependency y)
        {
            if (ReferenceEquals(x, y)) return 0;

            if (x.MustBeAfter(y)) return 1;
            if (y.MustBeAfter(x)) return -1;

            return 0;
        }

        public IList<Type> PolicyTypes
        {
            get { return _policyTypes; }
        }

        public IEnumerable<IFileDependency> AllDependencies()
        {
            return _objects.OfType<IFileDependency>();
        }

        public void Alias(string name, string alias)
        {
            _objects[name].AddAlias(alias);
        }

        public void Dependency(string dependent, string dependency)
        {
            _rules.Fill(new DependencyRule()
                        {
                            Dependency = dependency,
                            Dependent = dependent
                        });
        }

        public void Extension(string extender, string @base)
        {
            _extenders.Add(new Extension(){
                Base = @base,
                Extender = extender
            });
        }

        public void AddToSet(string setName, string name)
        {
            _sets[setName].Add(name);
        }

        public void Preceeding(string beforeName, string afterName)
        {
            _preceedings.Add(new PreceedingAsset(){
                Before = beforeName,
                After = afterName
            });
        }

        public void ForCombinations(Action<string, IList<string>> combinations)
        {
            _combos.Each(combinations);
        }

        public IEnumerable<string> NamesForCombination(string comboName)
        {
            return _combos[comboName];
        }

        public void AddToCombination(string comboName, string names)
        {
            _combos[comboName].Fill(names.ToDelimitedArray());
        }

        public void ApplyPolicy(string typeName)
        {
            var type = Type.GetType(typeName, false);
            if (type == null)
            {
                var comboType = typeof(CombineAllScriptFiles);
                var tryName = "{0}.{1},{2}".ToFormat(comboType.Namespace, typeName, comboType.Assembly.GetName().Name);

                type = Type.GetType(tryName);
            }

            if (type == null)
            {
                throw new ArgumentOutOfRangeException("Type {0} cannot be found".ToFormat(typeName));
            }

            _policyTypes.Fill(type);
        }

        public IEnumerable<IFileDependency> GetAssets(IEnumerable<string> names)
        {
            // TODO -- Memoize this.  Seriously.
            return new AssetGatherer(this, names).Gather();
        }

        public AssetSet AssetSetFor(string someName)
        {
            return _sets[someName];
        }


        // TODO -- try to find circular dependencies and log to the Package log
        public void CompileDependencies(IPackageLog log)
        {
            _sets.Each(set => set.FindScripts(this));
            _rules.Each(rule =>
            {
                var dependency = ObjectFor(rule.Dependency);
                var dependent = ObjectFor(rule.Dependent);

                dependency.AllFileDependencies().Each(dependent.AddDependency);
            });

            _extenders.Each(x =>
            {
                var @base = FileDependencyFor(x.Base);
                var extender = FileDependencyFor(x.Extender);

                @base.AddExtension(extender);
                extender.AddDependency(@base);
            });

            _preceedings.Each(x =>
            {
                var before = FileDependencyFor(x.Before);
                var after = FileDependencyFor(x.After);

                after.MustBePreceededBy(before);
            });
        }

        // Find by name or by alias
        public IRequestedAsset ObjectFor(string name)
        {
            return _objects[name];
        }

        public IFileDependency FileDependencyFor(string name)
        {
            return (IFileDependency) ObjectFor(name);
        }
    }
}