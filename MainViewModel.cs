using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Xml;
using System.Xml.Linq;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using Microsoft.WindowsAPICodePack.Dialogs;
using Microsoft.WindowsAPICodePack.Shell.Interop;
using ResxUnusedFinder.Properties;

namespace ResxUnusedFinder
{
    public class MainViewModel : ViewModelBase
    {
        private XDocument resourceDocument;
        private HashSet<string> resourceKeys;
        private List<string> files;
        private List<string> extensionList;
        private List<string> referenceFormatList;
        private List<string> excludePrefixesList;
        private Dictionary<string, Regex> regexCache;

        public MainViewModel()
        {
            this.ProjectFolder = Settings.Default.ProjectFolder;
            this.ResourceFile = Settings.Default.ResourceFile;
            this.Extensions = Settings.Default.Extensions;
            this.ExcludePrefixes = Settings.Default.ExcludePrefixes;
            this.UseRegex = Settings.Default.UseRegex;

            this.regexCache = new Dictionary<string, Regex>();

            StringCollection settingsReferenceFormatList = Settings.Default.ReferenceFormatsList;
            if (settingsReferenceFormatList == null || settingsReferenceFormatList.Count == 0)
            {
                string oldReferenceFormats = Settings.Default.ReferenceFormats;

                if (string.IsNullOrEmpty(oldReferenceFormats))
                {
                    this.ReferenceFormats = new ObservableCollection<ReferenceFormatViewModel>
                    {
                        new ReferenceFormatViewModel { Value = "AppResources.%" }
                    };
                }
                else
                {
                    this.ReferenceFormats = new ObservableCollection<ReferenceFormatViewModel>(
                        oldReferenceFormats
                            .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(format => new ReferenceFormatViewModel { Value = format }));
                }
            }
            else
            {
                this.ReferenceFormats = new ObservableCollection<ReferenceFormatViewModel>();
                foreach (string referenceFormat in Settings.Default.ReferenceFormatsList)
                {
                    this.ReferenceFormats.Add(new ReferenceFormatViewModel { Value = referenceFormat });
                }
            }

            this.RaisePropertyChanged(nameof(this.CanRemoveReferenceFormat));

            if (string.IsNullOrEmpty(this.Extensions))
            {
                this.Extensions = ".cs,.xaml";
            }
        }

        public void OnClose()
        {
            var referenceFormatsList = new StringCollection();
            foreach (string format in this.ReferenceFormats.Select(f => f.Value))
            {
                referenceFormatsList.Add(format);
            }

            Settings.Default.ProjectFolder = this.ProjectFolder;
            Settings.Default.ResourceFile = this.ResourceFile;
            Settings.Default.Extensions = this.Extensions;
            Settings.Default.ReferenceFormatsList = referenceFormatsList;
            Settings.Default.ExcludePrefixes = this.ExcludePrefixes;
            Settings.Default.UseRegex = this.UseRegex;
        }

        private string projectFolder;
        public string ProjectFolder
        {
            get { return this.projectFolder; }
            set { this.Set(ref this.projectFolder, value); }
        }

        private string resourceFile;
        public string ResourceFile
        {
            get { return this.resourceFile; }
            set { this.Set(ref this.resourceFile, value); }
        }

        private string extensions;
        public string Extensions
        {
            get { return this.extensions; }
            set { this.Set(ref this.extensions, value); }
        }

        public ObservableCollection<ReferenceFormatViewModel> ReferenceFormats { get; }

        private string excludePrefixes;
        public string ExcludePrefixes
        {
            get { return this.excludePrefixes; }
            set { this.Set(ref this.excludePrefixes, value); }
        }

        private ObservableCollection<StringResource> unusedResources;
        public ObservableCollection<StringResource> UnusedResources
        {
            get { return this.unusedResources; }
            set { this.Set(ref this.unusedResources, value); }
        }

        private string status;
        public string Status
        {
            get { return this.status; }
            set { this.Set(ref this.status, value); }
        }

        private bool working;
        public bool Working
        {
            get { return this.working; }
            set
            {
                this.Set(ref this.working, value);

                DispatchService.BeginInvoke(() =>
                {
                    this.RefreshCommand.RaiseCanExecuteChanged();
                    this.CopyCommand.RaiseCanExecuteChanged();
                    this.DeleteAllCommand.RaiseCanExecuteChanged();
                    this.DeleteSelectedCommand.RaiseCanExecuteChanged();
                    this.ExcludeSelectedCommand.RaiseCanExecuteChanged();
                    this.BrowseFolderCommand.RaiseCanExecuteChanged();
                    this.BrowseResourceFileCommand.RaiseCanExecuteChanged();
                });
            }
        }

        public bool ItemsSelected
        {
            get { return this.UnusedResources != null && this.UnusedResources.Any(r => r.IsSelected); }
        }

        public bool CanRemoveReferenceFormat
        {
            get { return this.ReferenceFormats.Count > 1; }
        }

        private bool useRegex;
        public bool UseRegex
        {
            get { return this.useRegex; }
            set { this.Set(ref this.useRegex, value); }
        }

        private RelayCommand browseFolderCommand;
        public RelayCommand BrowseFolderCommand
        {
            get
            {
                return this.browseFolderCommand ?? (this.browseFolderCommand = new RelayCommand(
                    () =>
                    {
                        var dialog = new CommonOpenFileDialog();
                        dialog.IsFolderPicker = true;
                        dialog.EnsurePathExists = true;
                        dialog.EnsureValidNames = true;
                        dialog.Multiselect = false;

                        if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                        {
                            this.ProjectFolder = dialog.FileName;
                        }
                    },
                    () =>
                    {
                        return !this.Working;
                    }));
            }
        }

        private RelayCommand browseResourceFileCommand;
        public RelayCommand BrowseResourceFileCommand
        {
            get
            {
                return this.browseResourceFileCommand ?? (this.browseResourceFileCommand = new RelayCommand(
                    () =>
                    {
                        var dialog = new CommonOpenFileDialog();
                        dialog.IsFolderPicker = false;
                        dialog.EnsurePathExists = true;
                        dialog.EnsureValidNames = true;
                        dialog.Multiselect = false;
                        dialog.Filters.Add(new CommonFileDialogFilter("RESX file", ".resx"));

                        if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                        {
                            this.ResourceFile = dialog.FileName;
                        }
                    },
                    () =>
                    {
                        return !this.Working;
                    }));
            }
        }

        private RelayCommand addReferenceFormatCommand;
        public RelayCommand AddReferenceFormatCommand
        {
            get
            {
                return this.addReferenceFormatCommand ?? (this.addReferenceFormatCommand = new RelayCommand(
                    () =>
                    {
                        this.ReferenceFormats.Add(new ReferenceFormatViewModel { Value = string.Empty });
                        this.RaisePropertyChanged(nameof(this.CanRemoveReferenceFormat));
                    }));
            }
        }

        private RelayCommand<ReferenceFormatViewModel> removeReferenceFormatCommand;
        public RelayCommand<ReferenceFormatViewModel> RemoveReferenceFormatCommand
        {
            get
            {
                return this.removeReferenceFormatCommand ?? (this.removeReferenceFormatCommand = new RelayCommand<ReferenceFormatViewModel>(
                    vm =>
                    {
                        this.ReferenceFormats.Remove(vm);
                        this.RaisePropertyChanged(nameof(this.CanRemoveReferenceFormat));
                    }));
            }
        }

        private RelayCommand refreshCommand;
        public RelayCommand RefreshCommand
        {
            get
            {
                return this.refreshCommand ?? (this.refreshCommand = new RelayCommand(
                    () =>
                    {
                        Task.Run(() =>
                        {
                            this.RefreshUnusedList();
                        });
                    },
                    () =>
                    {
                        return !this.Working;
                    }));
            }
        }

        private RelayCommand copyCommand;
        public RelayCommand CopyCommand
        {
            get
            {
                return this.copyCommand ?? (this.copyCommand = new RelayCommand(
                    () =>
                    {
                        string clipboardText = string.Join(Environment.NewLine, this.UnusedResources.Select(r => r.Key));
                        if (ClipboardService.SetText(clipboardText))
                        {
                            MessageBox.Show("Copied " + this.UnusedResources.Count + " key(s) to the clipboard.");
                        }
                    },
                    () =>
                    {
                        return !this.Working && this.UnusedResources != null && this.UnusedResources.Count > 0;
                    }));
            }
        }

        private RelayCommand deleteSelectedCommand;
        public RelayCommand DeleteSelectedCommand
        {
            get
            {
                return this.deleteSelectedCommand ?? (this.deleteSelectedCommand = new RelayCommand(
                    () =>
                    {
                        this.Delete(new HashSet<string>(this.UnusedResources.Where(r => r.IsSelected).Select(r => r.Key)));
                    },
                    () =>
                    {
                        return !this.Working;
                    }));
            }
        }

        private RelayCommand deleteAllCommand;
        public RelayCommand DeleteAllCommand
        {
            get
            {
                return this.deleteAllCommand ?? (this.deleteAllCommand = new RelayCommand(
                    () =>
                    {
                        if (MessageBox.Show("Are you sure you want to delete " + this.UnusedResources.Count + " resources?", "Confirm delete", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                        {
                            Task.Run(() =>
                            {
                                this.Delete(new HashSet<string>(this.resourceKeys));
                            });
                        }
                    },
                    () =>
                    {
                        return !this.Working && this.UnusedResources != null && this.UnusedResources.Count > 0;
                    }));
            }
        }

        private RelayCommand excludeSelectedCommand;
        public RelayCommand ExcludeSelectedCommand
        {
            get
            {
                return this.excludeSelectedCommand ?? (this.excludeSelectedCommand = new RelayCommand(
                    () =>
                    {
                        var selectedItems = this.UnusedResources.Where(r => r.IsSelected).ToList();
                        foreach (var selectedItem in selectedItems)
                        {
                            this.UnusedResources.Remove(selectedItem);
                            this.resourceKeys.Remove(selectedItem.Key);
                        }

                        this.Status = "Excluded " + selectedItems.Count + " item(s).";

                        this.CopyCommand.RaiseCanExecuteChanged();
                        this.DeleteAllCommand.RaiseCanExecuteChanged();
                    },
                    () =>
                    {
                        return !this.Working;
                    }));
            }
        }

        private RelayCommand<SelectionChangedEventArgs> onSelectionChangedCommand;
        public RelayCommand<SelectionChangedEventArgs> OnSelectionChangedCommand
        {
            get
            {
                return this.onSelectionChangedCommand ?? (this.onSelectionChangedCommand = new RelayCommand<SelectionChangedEventArgs>(e =>
                {
                    this.RaisePropertyChanged(() => this.ItemsSelected);
                }));
            }
        }

        private void RefreshUnusedList()
        {
            try
            {
                if (!this.ValidateRegexPatterns())
                {
                    return;
                }

                this.Working = true;

                this.PopulateSearchList();
                this.FindUnusedResources();

                this.Working = false;
            }
            catch (FileException exception)
            {
                HandleException(exception);
            }
            catch (ParseException exception)
            {
                HandleException(exception);
            }
        }

        private void PopulateSearchList()
        {
            this.Status = "Populating search list...";

            this.resourceKeys = new HashSet<string>();
            this.excludePrefixesList = this.ExcludePrefixes == null ? 
                new List<string>() :
                this.ExcludePrefixes.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToList();

            this.resourceDocument = XDocument.Load(this.ResourceFile);

            ForEveryDataElement(this.resourceDocument, dataElement =>
            {
                string name = GetName(dataElement);

                bool addName = true;
                foreach (string excludedPrefix in this.excludePrefixesList)
                {
                    if (name.StartsWith(excludedPrefix))
                    {
                        addName = false;
                        break;
                    }
                }

                if (addName)
                {
                    this.resourceKeys.Add(name);
                }
            });
        }

        private void FindUnusedResources()
        {
            this.PopulateFileList();

            this.Status = "Finding unused keys...";

            this.referenceFormatList = this.ReferenceFormats.Select(f => f.Value).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();

            foreach (string file in this.files)
            {
                this.RemoveFoundKeys(file);
            }

            // Remaining keys are unused
            var newUnusedResources = new ObservableCollection<StringResource>();
            foreach (string unusedKey in this.resourceKeys)
            {
                newUnusedResources.Add(new StringResource { Key = unusedKey });
            }

            this.UnusedResources = newUnusedResources;

            this.Status = "Getting resource values...";

            this.PopulateUnusedResourceValues();

            this.Status = "Found " + this.UnusedResources.Count + " unused resource(s).";
        }

        private bool ValidateRegexPatterns()
        {
            if (!this.UseRegex)
            {
                return true;
            }

            foreach (string format in this.ReferenceFormats.Select(f => f.Value))
            {
                string testPattern = format.Replace("%", "Test");
                try
                {
                    var regex = new Regex(testPattern);
                }
                catch (Exception)
                {
                    MessageBox.Show("Regex is not valid: " + format);
                    return false;
                }
            }

            return true;
        }

        private void PopulateUnusedResourceValues()
        {
            ForEveryDataElement(this.resourceDocument, dataElement =>
            {
                var unusedResource = this.UnusedResources.FirstOrDefault(r => r.Key == GetName(dataElement));
                if (unusedResource != null)
                {
                    var valueElement = dataElement.Element("value");
                    if (valueElement != null)
                    {
                        unusedResource.Value = valueElement.Value;
                    }
                }
            });
        }

        private void PopulateFileList()
        {
            this.Status = "Populating file list...";

            this.extensionList = this.Extensions.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(e => CleanExtension(e.Trim())).ToList();
            this.files = new List<string>();

            try
            {
                this.PopulateFileList(this.ProjectFolder);
            }
            catch (IOException exception)
            {
                throw new FileException("Could not populate file list.", exception);
            }
            catch (UnauthorizedAccessException exception)
            {
                throw new FileException("Could not populate file list.", exception);
            }
        }

        private static string CleanExtension(string extension)
        {
            if (extension.StartsWith("."))
            {
                return extension;
            }

            return "." + extension;
        }

        private void PopulateFileList(string directoryPath)
        {
            foreach (string file in Directory.GetFiles(directoryPath))
            {
                foreach (string extension in this.extensionList)
                {
                    if (file.EndsWith(extension))
                    {
                        this.files.Add(file);
                        break;
                    }
                }
            }

            foreach (string subDirectory in Directory.GetDirectories(directoryPath))
            {
                this.PopulateFileList(subDirectory);
            }
        }

        // Finds instances of resourceKeys in the given file and removes them from the collection if they are found
        private void RemoveFoundKeys(string filePath)
        {
            string fileText = File.ReadAllText(filePath, Encoding.Default);
            var foundResources = new List<string>();

            foreach (string key in this.resourceKeys)
            {
                foreach (string referenceFormat in this.referenceFormatList)
                {
                    string searchString = referenceFormat.Replace("%", key);

                    bool fileHasMatch;
                    if (this.UseRegex)
                    {
                        Regex regex;
                        if (!this.regexCache.TryGetValue(searchString, out regex))
                        {
                            regex = new Regex(searchString);
                            this.regexCache.Add(searchString, regex);
                        }

                        fileHasMatch = regex.IsMatch(fileText);
                    }
                    else
                    {
                        fileHasMatch = fileText.Contains(searchString);
                    }

                    if (fileHasMatch)
                    {
                        foundResources.Add(key);
                        break;
                    }
                }
            }

            foreach (string key in foundResources)
            {
                this.resourceKeys.Remove(key);
            }
        }

        private void Delete(HashSet<string> stringsToDelete)
        {
            try
            {
                this.Working = true;

                int count = stringsToDelete.Count;
                this.Status = "Deleting " + count + " unused resource(s)...";

                var document = XDocument.Load(this.ResourceFile);
                ForEveryDataElement(document, dataElement =>
                {
                    if (stringsToDelete.Contains(GetName(dataElement)))
                    {
                        dataElement.Remove();
                    }
                });

                try
                {
                    document.Save(this.ResourceFile);
                }
                catch (IOException exception)
                {
                    throw new FileException("Error saving resource file.", exception);
                }
                catch (UnauthorizedAccessException exception)
                {
                    throw new FileException("Error saving resource file.", exception);
                }
                catch (XmlException exception)
                {
                    throw new FileException("Error saving resource file.", exception);
                }

                // Remove them from the collections
                foreach (string deletedKey in stringsToDelete)
                {
                    this.resourceKeys.Remove(deletedKey);
                }

                DispatchService.BeginInvoke(() =>
                {
                    for (int i = this.UnusedResources.Count - 1; i >= 0; i--)
                    {
                        if (stringsToDelete.Contains(this.UnusedResources[i].Key))
                        {
                            this.UnusedResources.RemoveAt(i);
                        }
                    }
                });

                this.Status = "Deleted " + count + " unused resource(s).";

                this.Working = false;
            }
            catch (FileException exception)
            {
                HandleException(exception);
            }
            catch (ParseException exception)
            {
                HandleException(exception);
            }
        }

        private static void ForEveryDataElement(XDocument document, Action<XElement> action)
        {
            if (document.Root == null)
            {
                throw new ParseException("No root element found.");
            }

            foreach (var dataElement in document.Root.Elements("data").ToList())
            {
                action(dataElement);
            }
        }

        private static string GetName(XElement dataElement)
        {
            var nameAttribute = dataElement.Attribute("name");
            if (nameAttribute == null)
            {
                throw new ParseException("Name attribute missing on data.");
            }

            return nameAttribute.Value;
        }

        private static void HandleException(Exception exception)
        {
            DispatchService.BeginInvoke(() =>
            {
                MessageBox.Show(exception.Message);
            });
        }
    }
}
