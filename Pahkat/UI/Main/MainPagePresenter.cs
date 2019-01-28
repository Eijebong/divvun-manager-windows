﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Pahkat.Extensions;
using Pahkat.Models;
using Pahkat.Service;
using Pahkat.Sdk;
using Pahkat.UI.Shared;

namespace Pahkat.UI.Main
{
    public class MainPagePresenter
    {
        private ObservableCollection<RepoTreeItem> _tree =
            new ObservableCollection<RepoTreeItem>();
        
        private IMainPageView _view;
        //private RepositoryService _repoServ;
        private IPackageService _pkgServ;
        private IPackageStore _store;

        private RepositoryIndex[] _currentRepos;
        private string _searchText;

        private IDisposable BindPackageToggled(IMainPageView view, IPackageStore store)
        {
            return view.OnPackageToggled()
                .Select(item => PackageStoreAction.TogglePackage(
                    item.Key,
                    _pkgServ.DefaultPackageAction(item.Key),
                    !item.IsSelected))
                .Subscribe(store.Dispatch);
        }
        
        private IDisposable BindGroupToggled(IMainPageView view, IPackageStore store)
        {
            return view.OnGroupToggled()
                .Select(item =>
                {
                    return PackageStoreAction.ToggleGroup(
                        item.Items.Select(x => new PackageActionInfo(x.Key, _pkgServ.DefaultPackageAction(x.Key))).ToArray(),
                        !item.IsGroupSelected);
                })
                .Subscribe(store.Dispatch);
        }

        private IDisposable BindPrimaryButton(IMainPageView view)
        {
            return view.OnPrimaryButtonPressed()
                .Subscribe(_ => view.ShowDownloadPage());
        }

        private IDisposable BindSearchTextChanged(IMainPageView view)
        {
            return view.OnSearchTextChanged()
                .Subscribe((text) =>
                {
                    _searchText = text;
                    RefreshPackageList();
                });
        }

        private IEnumerable<PackageCategoryTreeItem> FilterByCategory(RepositoryIndex repo)
        {
            var map = new Dictionary<string, List<PackageMenuItem>>();
            var packages = ApplySearchText(repo.Packages.Values);

            foreach (var package in packages)
            {
                if (!map.ContainsKey(package.Category))
                {
                    map[package.Category] = new List<PackageMenuItem>();
                }

                map[package.Category].Add(new PackageMenuItem(repo.AbsoluteKeyFor(package), package, _pkgServ, _store));
            }

            var categories = new ObservableCollection<PackageCategoryTreeItem>(map.OrderBy(x => x.Key).Select(x =>
            {
                x.Value.Sort();
                var items = new ObservableCollection<PackageMenuItem>(x.Value);
                return new PackageCategoryTreeItem(_store, x.Key, items);
            }));

            return categories;
        }

        private IEnumerable<PackageCategoryTreeItem> FilterByLanguage(RepositoryIndex repo)
        {
            var map = new Dictionary<string, List<PackageMenuItem>>();
            var packages = ApplySearchText(repo.Packages.Values);

            foreach (var package in packages)
            {
                foreach (var bcp47 in package.Languages)
                {
                    if (!map.ContainsKey(bcp47))
                    {
                        map[bcp47] = new List<PackageMenuItem>();
                    }

                    map[bcp47].Add(new PackageMenuItem(repo.AbsoluteKeyFor(package), package, _pkgServ, _store));
                }
            }

            var languages = new ObservableCollection<PackageCategoryTreeItem>(map.OrderBy(x => x.Key).Select(x =>
            {
                x.Value.Sort();
                var items = new ObservableCollection<PackageMenuItem>(x.Value);
                return new PackageCategoryTreeItem(_store, Util.Util.GetCultureDisplayName(x.Key), items);
            }));

            return languages;
        }

        private IEnumerable<Package> ApplySearchText(IEnumerable<Package> packages)
        {
            return packages.Where(x =>
            {
                return string.IsNullOrWhiteSpace(_searchText)
                    ? true
                    : x.NativeName.ToLowerInvariant().Contains(_searchText.ToLowerInvariant());
            });
        }
        
        private void RefreshPackageList()
        {
            _tree.Clear();
            
            if (_currentRepos == null)
            {
                Console.WriteLine("Repository empty.");
                _view.UpdateTitle(Strings.AppName);
                return;
            }

            foreach (var repo in _currentRepos)
            {
                IEnumerable<PackageCategoryTreeItem> items;
                switch (repo.Meta.PrimaryFilter)
                {
                    case RepositoryMeta.Filter.Language:
                        items = FilterByLanguage(repo);
                        break;
                    case RepositoryMeta.Filter.Category:
                    default:
                        items = FilterByCategory(repo);
                        break;
                }

                var item = new RepoTreeItem(
                    repo.Meta.NativeName,
                    new ObservableCollection<PackageCategoryTreeItem>(items)
                );
                _tree.Add(item);
            }

            _view.UpdateTitle($"{Strings.AppName}");
            Console.WriteLine("Added packages.");
        }

        //private IDisposable BindUpdatePackageList(RpcService rpc, IPackageService pkgServ, IPackageStore store)
        //{
        //    return rpc.Repository()
        //    return repoServ.System
        //        .Select(x => x.RepoResult?.Repository)
        //        .NotNull()
        //        .DistinctUntilChanged()
        //        .Subscribe(repo =>
        //        {
        //            _currentRepo = repo;
        //            RefreshPackageList();
        //        }, _view.HandleError);
        //}

        private void GeneratePrimaryButtonLabel(Dictionary<AbsolutePackageKey, PackageActionInfo> packages)
        {
            if (packages.Count > 0)
            {
                string s;

                if (packages.All(x => x.Value.Action == PackageActionType.Install))
                {
                    s = string.Format(Strings.InstallNPackages, packages.Count);
                }
                else if (packages.All(x => x.Value.Action == PackageActionType.Uninstall))
                {
                    s = string.Format(Strings.UninstallNPackages, packages.Count);
                }
                else
                {
                    s = string.Format(Strings.InstallUninstallNPackages, packages.Count);
                }

                _view.UpdatePrimaryButton(true, s);
            }
            else
            {
                _view.UpdatePrimaryButton(false, Strings.NoPackagesSelected);
            }
        }

        private IDisposable BindPrimaryButtonLabel(IMainPageView view, IPackageStore store)
        {
            // Can't use distinct until changed here because HashSet is never reset
            return store.State
                .Select(state => state.SelectedPackages)
                .Subscribe(GeneratePrimaryButtonLabel);
        }

        public void SetRepos(RepositoryIndex[] repos)
        {
            _currentRepos = repos;
            RefreshPackageList();
        }

        public MainPagePresenter(IMainPageView view, IPackageService pkgServ, IPackageStore store)
        {
            //_repoServ = repoServ;
            _pkgServ = pkgServ;
            _view = view;
            _store = store;

        }

        public IDisposable Start()
        {
            _view.UpdateTitle($"{Strings.AppName} - {Strings.Loading}");
            _view.SetPackagesModel(_tree);

            return new CompositeDisposable(
                BindPrimaryButtonLabel(_view, _store),
                //BindUpdatePackageList(_repoServ, _pkgServ, _store),
                BindPackageToggled(_view, _store),
                BindGroupToggled(_view, _store),
                BindPrimaryButton(_view),
                BindSearchTextChanged(_view)
            );
        }
    }
}