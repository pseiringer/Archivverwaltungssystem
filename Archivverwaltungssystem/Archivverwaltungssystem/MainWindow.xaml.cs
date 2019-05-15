using System;
using System.Collections.Generic;
using System.Data.Entity.Validation;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ArchiveDatabase;
using ArchiveDatabase.DatabasePOCOs;
using Xceed.Wpf;

namespace Archivverwaltungssystem
{
    public partial class MainWindow : Window
    {
        private ArchiveContext db;

        private ContextMenu structureContextMenu;
        private MenuItem structureContextMenuEdit;

        private List<Category> currentCategories = new List<Category>();
        private List<Document> currentDocuments = new List<Document>();

        private Category editedCategory = null;
        private Document editedDocument = null;
        private UIElement lastOpenedElement = null;

        private bool isEditing = false;
        private bool editingCancelled = false;

        private const string NO_CATEGORY = "Keine (Hauptkategorie)";
        private const string ALL_CATEGORIES = "Alle Kategorien";

        private const string STRUCTURE_CONTEXT_MENU_EDIT = "Bearbeiten";
        private const string STRUCTURE_CONTEXT_MENU_DELETE = "Löschen";

        private const string MESSAGE_CAT_HAS_CHILDREN = "Die ausgewählte Kategorie besitzt Kind-Elemente!\n\r" +
                                                        "Wollen sie diese Elemente mitverschieben,\n\r" +
                                                        "der nächsthöheren Kategorie zuordnen oder Löschen?";
        private const string MESSAGE_CAT_HAS_CHILDREN_TITLE = "Kind-Elemente Vorhanden";
        private const string MESSAGE_CAT_HAS_CHILDREN_DELETE = "Die ausgewählte Kategorie besitzt Kind-Elemente!\n\r" +
                                                        "Wollen sie diese Elemente der nächsthöheren Kategorie zuordnen " +
                                                        "oder Löschen?";

        private const string CAT_HAS_CHILDREN_DELETE_BUTTON = "Löschen";
        private const string CAT_HAS_CHILDREN_MOVE_BUTTON = "Verschieben";
        private const string CAT_HAS_CHILDREN_MOVETOSUPER_BUTTON = "nächsthöherer Kategorie zuordnen";

        private const string SEED_FILE_PATH = @"C:\\Archivverwaltung\\SeedFile.csv";

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            db = new ArchiveContext();

            seedDbFromCSV();

            ClearAll();
            ReloadAll();
        }


        /* Control Elements */
        private void BtnSearchTabClick(object sender, RoutedEventArgs e)
        {
            if (!CheckAllSaved()) return;

            CollapseAndClearAll();
            Search.Visibility = Visibility.Visible;
            btnSearchTab.Style = Resources["ActiveRoundedButtonStyle"] as Style;
        }
        private void BtnCreateTabClick(object sender, RoutedEventArgs e)
        {
            if (!CheckAllSaved()) return;

            CollapseAndClearAll();
            Create.Visibility = Visibility.Visible;
            btnCreateTab.Style = Resources["ActiveRoundedButtonStyle"] as Style;
        }
        private void BtnStructureTabClick(object sender, RoutedEventArgs e)
        {
            if (!CheckAllSaved()) return;

            CollapseAndClearAll();
            Structure.Visibility = Visibility.Visible;
            btnStructureTab.Style = Resources["ActiveRoundedButtonStyle"] as Style;
        }

        private bool CheckAllSaved()
        {
            if (!CreateTabSaved() || !EditTabSaved())
            {
                var res = MessageBox.Show("Alle Eingaben die sie nicht gespeichert haben verfallen. Wollen sie fortfahren?",
                    "Nicht gespeicherte Eingaben", MessageBoxButton.OKCancel, MessageBoxImage.Information);
                switch (res)
                {
                    case MessageBoxResult.OK:
                        return true;
                    case MessageBoxResult.Cancel:
                        return false;
                    case MessageBoxResult.None:
                        return false;
                }
            }

            return true;
        }

        private void CollapseAndClearAll()
        {
            ClearAll();
            if (isEditing)
            {
                editingCancelled = true;
                StopEditing();
            }
            Search.Visibility = Visibility.Collapsed;
            Create.Visibility = Visibility.Collapsed;
            Structure.Visibility = Visibility.Collapsed;
            btnSearchTab.Style = Resources["RoundedButtonStyle"] as Style;
            btnCreateTab.Style = Resources["RoundedButtonStyle"] as Style;
            btnStructureTab.Style = Resources["RoundedButtonStyle"] as Style;
        }

        private void ClearAll()
        {
            ClearCreateCategoryTab();
            ClearCreateDocumentTab();
            ClearEditCategoryTab();
            ClearEditDocumentTab();

            ClearCurrentVariables();
        }

        private void ClearCurrentVariables()
        {
            currentCategories = new List<Category>();
            currentDocuments = new List<Document>();
        }

        private void ReloadAll()
        {
            ReloadStructure();
            ReloadCategoryBoxes();

        }

        private void ReloadForDocumentChange()
        {
            ReloadStructure();
            BtnSearchClicked(null, null);
        }

        private void ReloadForCategoryChange()
        {
            ReloadAll();
            BtnSearchClicked(null, null);
        }

        private void ReloadCategoryBoxes()
        {
            var orderedCategories = GetOrderedCategoryNumbers();

            var orderedCatsWithNull = new List<string>();
            orderedCatsWithNull.AddRange(orderedCategories);
            orderedCatsWithNull.Insert(0, NO_CATEGORY);

            var searchOrderedCats = new List<string>();
            searchOrderedCats.AddRange(orderedCategories);
            searchOrderedCats.Insert(0, ALL_CATEGORIES);

            SearchCategory.ItemsSource = searchOrderedCats;

            CreateDocumentCategory.ItemsSource = orderedCategories;
            CreateCategoryCategory.ItemsSource = orderedCatsWithNull;
            EditDocumentCategory.ItemsSource = orderedCategories;
            EditCategoryCategory.ItemsSource = orderedCatsWithNull;
        }

        private List<string> GetOrderedCategoryNumbers()
        {
            var res = db.Categories.ToList()
                .OrderBy(x => x.CategoryNumber)
                .Select(x => x.ToString())
                .ToList();
            return res;
        }

        /* Search Elements */
        private void BtnSearchClicked(object sender, RoutedEventArgs e)
        {
            var selectedCatString = SearchCategory.SelectedItem.ToString();
            var catString = ToSuperCatString(selectedCatString);
            Category cat = null;
            if (catString != null) cat = db.Categories.FirstOrDefault(y => y.CategoryNumber == catString);

            var docs = db.Documents.ToList();
            if (cat != null)
            {
                docs = SelectAllDocumentsInCat(cat, docs);
            }

            string search = SearchText.Text;

            var dateFrom = SearchDateFrom.SelectedDate;
            var dateTo = SearchDateTo.SelectedDate;

            var temp = docs.Where(x =>
            {
                bool res = x.DocumentName.Contains(search) || x.DocumentNotes.Contains(search);
                if (dateFrom != null) res = res && x.DocumentDate > dateFrom;
                if (dateTo != null) res = res && x.DocumentDate < dateTo;
                return res;
            })
                                        .OrderBy(x => x.SuperCategory.CategoryNumber)
                                        .ThenBy(x => x.DocumentName)
                                        .Select(x => new DisplayDocument { DocumentId = x.DocumentId, CategoryNumber = x.SuperCategory.CategoryNumber, DocumentName = x.DocumentName })
                                        .ToList();
            SearchResults.ItemsSource = temp;
        }

        private List<Document> SelectAllDocumentsInCat(Category cat, List<Document> docs)
        {

            var result = docs.Where(x =>
            {
                bool res = (x.SuperCategory == cat);

                return res;
            }).ToList();


            var cats = db.Categories.Where(x => x.SuperCategory.CategoryId == cat.CategoryId).ToList();

            foreach (var currentCat in cats)
            {
                result.AddRange(SelectAllDocumentsInCat(currentCat, docs));
            }

            return result;
        }


        private void SearchResultsMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            SearchContextMenuEditClicked(sender, e);
        }

        private void UnfoldDocAndOpenStructure(int documentId)
        {
            UnfoldIdAndOpenStructure(documentId, true);
        }

        private void UnfoldIdAndOpenStructure(int documentId, bool isDocument)
        {
            UnfoldIdInTreeView(documentId, isDocument);
            BtnStructureTabClick(null, null);
        }


        /* Create Elements */
        private void ClearCreateCategoryTab()
        {
            CreateCategoryName.Text = "";
            CreateCategoryCategory.SelectedIndex = 0;
        }

        private void ClearCreateDocumentTab()
        {
            CreateDocumentName.Text = "";
            CreateDocumentNotes.Text = "";
            CreateDocumentDate.SelectedDate = DateTime.Today;
            CreateDocumentCategory.SelectedIndex = 0;
        }

        private bool CreateTabSaved()
        {
            return CreateCategoryName.Text == ""
                && (CreateCategoryCategory.SelectedIndex == 0 || CreateCategoryCategory.SelectedItem == null)
                && CreateDocumentName.Text == ""
                && CreateDocumentNotes.Text == ""
                && CreateDocumentDate.SelectedDate == DateTime.Today
                && (CreateDocumentCategory.SelectedIndex == 0 || CreateDocumentCategory.SelectedItem == null);
        }

        private void BtnCreateDocumentCancelClicked(object sender, RoutedEventArgs e)
        {
            ClearCreateDocumentTab();
        }

        private void BtnCreateDocumentSaveClicked(object sender, RoutedEventArgs e)
        {
            var name = CreateDocumentName.Text;
            var notes = CreateDocumentNotes.Text;
            var date = CreateDocumentDate.SelectedDate;
            var selectedCategory = CreateDocumentCategory.SelectedItem;
            if (!CheckEnteredDocumentValid(name, date, selectedCategory)) return;

            var superCatString = ToSuperCatString(selectedCategory.ToString());
            var superCat = db.Categories.FirstOrDefault(y => y.CategoryNumber == superCatString);

            var doc = new Document() { DocumentName = name, DocumentNotes = notes, SuperCategory = superCat, DocumentDate = (DateTime)date };
            db.Documents.Add(doc);
            try
            {
                db.SaveChanges();
            }
            catch (DbEntityValidationException exc)
            {

                foreach (var eve in exc.EntityValidationErrors)
                {
                    Console.WriteLine("Entity of type \"{0}\" in state \"{1}\" has the following validation errors:",
                        eve.Entry.Entity.GetType().Name, eve.Entry.State);
                    foreach (var ve in eve.ValidationErrors)
                    {
                        Console.WriteLine("- Property: \"{0}\", Error: \"{1}\"",
                            ve.PropertyName, ve.ErrorMessage);
                    }
                }

                throw;
            }
            ClearCreateDocumentTab();
            ReloadForDocumentChange();
        }

        private bool CheckEnteredDocumentValid(string name, DateTime? date, object category)
        {
            if (name == "")
            {
                var res = MessageBox.Show("Bitte geben Sie zuerst einen Dokumentennamen ein.",
                    "Kein Dokumentenname", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (date == null || date.Value.Year <= 1)
            {
                var res = MessageBox.Show("Bitte geben Sie zuerst ein valides Datum ein.",
                    "Kein Datum", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (category == null)
            {
                var res = MessageBox.Show("Es muss zuerst eine Kategorie gewählt oder erstellt werden.",
                    "Keine Kategorie", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private string ToSuperCatString(string v)
        {
            if (v == NO_CATEGORY) return null;
            if (v == ALL_CATEGORIES) return null;
            return v.Split(':')[0];
        }

        private void BtnCreateCategoryCancelClicked(object sender, RoutedEventArgs e)
        {
            ClearCreateCategoryTab();
        }

        private void BtnCreateCategorySaveClicked(object sender, RoutedEventArgs e)
        {
            var name = CreateCategoryName.Text;
            if (name == "")
            {
                var res = MessageBox.Show("Bitte geben Sie zuerst einen Kategorienamen ein.",
                    "Kein Kategoriename", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var superCatString = ToSuperCatString(CreateCategoryCategory.SelectedItem.ToString());
            var superCat = db.Categories.FirstOrDefault(y => y.CategoryNumber == superCatString);
            var nextCat = GetHighestCategoryNumberOfCategory(superCatString);

            var cat = new Category() { CategoryName = name, CategoryNumber = nextCat, SuperCategory = superCat };
            db.Categories.Add(cat);
            db.SaveChanges();
            ClearCreateCategoryTab();
            ReloadForCategoryChange();
        }

        private string GetHighestCategoryNumberOfCategory(string superCatString)
        {
            return GetHighestCategoryNumberOfCategory(superCatString, null, null);
        }

        private string GetHighestTempNumber()
        {
            var temps = db.Categories.Where(x => x.CategoryNumber.StartsWith("temp"))
                                .ToList();
            int num = 1;
            if (temps.Count() > 0)
                num = (temps.Select(x => int.Parse(x.CategoryNumber.Substring(4)))
                                .Max()
                                + 1);

            return "temp" + num;
        }

        private string GetHighestCategoryNumberOfCategory(string superCatString, List<Category> excludedCategories, List<Category> handledCategories)
        {
            // superCatString is equal to superCategory.CategoryNumber or ToSuperCatString(selectedComboBoxItem.ToString())
            int noOfNumberFields = 0;
            var nextCat = superCatString;
            var superCat = db.Categories.FirstOrDefault(y => y.CategoryNumber == superCatString);

            if (superCatString != null)
            {
                noOfNumberFields = superCatString.Split('.').Count();

                nextCat = superCatString;
                var lastCatIdsInSameSuper = db.Categories.Where(x => x.SuperCategory.CategoryId == superCat.CategoryId)
                    .ToList();
                if (excludedCategories != null)
                {
                    foreach (var excluded in excludedCategories)
                    {
                        if (lastCatIdsInSameSuper.Contains(excluded)) lastCatIdsInSameSuper.Remove(excluded);
                    }
                }

                if (handledCategories != null) lastCatIdsInSameSuper.AddRange(handledCategories);

                int highestCatNum = 0;

                if (lastCatIdsInSameSuper.Count > 0)
                {
                    var temp = lastCatIdsInSameSuper.Select(x => int.Parse(x.CategoryNumber.Split('.')[noOfNumberFields]));
                    highestCatNum = temp.Max();
                }

                nextCat = nextCat + "." + (highestCatNum + 1);
            }
            else
            {
                var includedCats = db.Categories.Where(x => x.SuperCategory == null).ToList();
                if (excludedCategories != null)
                {
                    foreach (var excluded in excludedCategories)
                    {
                        if (includedCats.Contains(excluded)) includedCats.Remove(excluded);
                    }
                }
                if (handledCategories != null) includedCats.AddRange(handledCategories);
                nextCat = (includedCats.Count() + 1).ToString();
            }

            return nextCat;

        }

        /* Structure Elements */
        private void ReloadStructure()
        {
            var root = RecursiveTreeViewItemsOfCategory(null, new TreeViewItem());
            Structure.ItemsSource = root.Items;

            InitStructureContextMenu(root.Items);

        }

        private TreeViewItem RecursiveTreeViewItemsOfCategory(Category superCat, TreeViewItem treeViewItem)
        {
            List<Category> subCats;
            if (superCat == null)
                subCats = db.Categories.Where(x => x.SuperCategory == superCat).ToList();
            else
                subCats = db.Categories.Where(x => x.SuperCategory.CategoryId == superCat.CategoryId).ToList();


            List<Document> subDocs;
            if (superCat == null)
                subDocs = db.Documents.Where(x => x.SuperCategory == superCat).ToList();
            else
                subDocs = db.Documents.Where(x => x.SuperCategory.CategoryId == superCat.CategoryId).ToList();

            var list = new List<TreeViewItem>();
            foreach (var cat in subCats)
            {
                var item = new TreeViewItem { Header = cat };
                item = RecursiveTreeViewItemsOfCategory(cat, item);
                list.Add(item);
            }
            foreach (var doc in subDocs)
            {
                list.Add(new TreeViewItem { Header = doc });
            }

            foreach (var item in list)
            {
                item.MouseLeftButtonDown += new MouseButtonEventHandler(TreeViewItemClick);
            }

            treeViewItem.ItemsSource = list;
            return treeViewItem;


        }

        private void TreeViewItemClick(object o, MouseButtonEventArgs args)
        {
            if (!(Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl) || Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)))
            {
                ClearCurrentVariables();
            }

            TreeViewItem clicked = (TreeViewItem)args.Source;
            var obj = clicked.Header;

            if (obj is Document) currentDocuments.Add((Document)obj);
            if (obj is Category) currentCategories.Add((Category)obj);
            
            clicked.Focusable = true;
            clicked.Focus();
            clicked.Focusable = false;
            args.Handled = true;
        }
        
        private bool IsMultiSelection() => currentCategories.Count + currentDocuments.Count >= 2;

        private void InitStructureContextMenu(ItemCollection items)
        {
            structureContextMenu = new ContextMenu();

            structureContextMenuEdit = new MenuItem();
            structureContextMenuEdit.Header = STRUCTURE_CONTEXT_MENU_EDIT;
            structureContextMenuEdit.Click += StructureContextMenuEditClicked;

            var item = new MenuItem();
            item.Header = STRUCTURE_CONTEXT_MENU_DELETE;
            item.Click += StructureContextMenuDeleteClicked;

            structureContextMenu.Items.Add(structureContextMenuEdit);
            structureContextMenu.Items.Add(item);


            foreach (TreeViewItem tvItem in items)
            {
                tvItem.ContextMenu = structureContextMenu;
            }
                       
        }

        private void UnfoldIdInTreeView(int id, bool isDocument)
        {
            CollapseWholeTreeView();
            if (id >= 0)
            {
                foreach (var item in Structure.Items)
                {
                    RecursiveUnfoldIdInTreeViewItem((TreeViewItem)item, id, isDocument);
                }
            }
        }
        

        private void RecursiveUnfoldIdInTreeViewItem(TreeViewItem item, int id, bool isDocument)
        {
            if (isDocument)
            {
                if (item.Header is Document) // Document can not have children
                {
                    if ((item.Header as Document).DocumentId == id)
                    {
                        item.IsExpanded = true;
                    }
                }
            }
            else
            {
                if (item.Header is Category)
                {
                    if ((item.Header as Category).CategoryId == id)
                    {
                        item.IsExpanded = true;
                    }
                }
            }


            foreach (var child in item.Items)
            {
                var tChild = (TreeViewItem)child;
                RecursiveUnfoldIdInTreeViewItem(tChild, id, isDocument);
                if (tChild.IsExpanded && !item.IsExpanded)
                {
                    item.IsExpanded = true;
                }
            }
        }

        private void CollapseWholeTreeView()
        {
            foreach (var item in Structure.Items)
            {
                RecursiveCollapseItemsOfTreeviewItem((TreeViewItem)item);
                ((TreeViewItem)item).IsExpanded = false;
            }
        }

        private void RecursiveCollapseItemsOfTreeviewItem(TreeViewItem item)
        {
            foreach (var i in item.Items)
            {
                RecursiveCollapseItemsOfTreeviewItem((TreeViewItem)i);
                ((TreeViewItem)i).IsExpanded = false;
            }
        }

        /* Edit Elements */

        private void StartEditingCategory(int catId, UIElement currentElement)
        {
            isEditing = true;
            editingCancelled = false;
            editedCategory = db.Categories.FirstOrDefault(x => x.CategoryId == catId);

            EditCategoryName.Text = editedCategory.CategoryName;
            if (editedCategory.SuperCategory != null)
                EditCategoryCategory.SelectedItem = editedCategory.SuperCategory.ToString();
            else
                EditCategoryCategory.SelectedItem = NO_CATEGORY;

            currentElement.Visibility = Visibility.Collapsed;
            lastOpenedElement = currentElement;

            EditCategory.Visibility = Visibility.Visible;
        }

        private void StartEditingDocument(int docId, UIElement currentElement)
        {
            isEditing = true;
            editingCancelled = false;
            editedDocument = db.Documents.FirstOrDefault(x => x.DocumentId == docId);

            EditDocumentName.Text = editedDocument.DocumentName;
            EditDocumentNotes.Text = editedDocument.DocumentNotes;
            EditDocumentDate.SelectedDate = editedDocument.DocumentDate;
            EditDocumentCategory.SelectedItem = editedDocument.SuperCategory.ToString();

            currentElement.Visibility = Visibility.Collapsed;
            lastOpenedElement = currentElement;

            EditDocument.Visibility = Visibility.Visible;
        }

        private void StopEditing()
        {

            EditCategory.Visibility = Visibility.Collapsed;
            EditDocument.Visibility = Visibility.Collapsed;

            isEditing = false;

            if (!editingCancelled)
            {
                if (editedDocument != null) ReloadForDocumentChange();
                else if (editedCategory != null) ReloadForCategoryChange();
            }
            else
            {
                editingCancelled = false;
            }

            if (lastOpenedElement != null)
            {
                lastOpenedElement.Visibility = Visibility.Visible;

                if (lastOpenedElement == Structure)
                {
                    if (editedCategory != null)
                    {
                        UnfoldIdInTreeView(editedCategory.CategoryId, false);
                    }
                    else if (editedDocument != null)
                    {
                        UnfoldIdInTreeView(editedDocument.DocumentId, true);
                    }
                }

                lastOpenedElement = null;
            }

            editedCategory = null;
            editedDocument = null;
        }

        private void ClearEditCategoryTab()
        {
            EditCategoryName.Text = "";
            EditCategoryCategory.SelectedIndex = 0;
        }

        private void ClearEditDocumentTab()
        {
            EditDocumentName.Text = "";
            EditDocumentNotes.Text = "";
            EditDocumentDate.SelectedDate = DateTime.Today;
            EditDocumentCategory.SelectedIndex = 0;
        }

        private bool EditTabSaved()
        {
            return EditCategoryName.Text == ""
                && (EditCategoryCategory.SelectedIndex == 0 || EditCategoryCategory.SelectedItem == null)
                && EditDocumentName.Text == ""
                && EditDocumentNotes.Text == ""
                && EditDocumentDate.SelectedDate == DateTime.Today
                && (EditDocumentCategory.SelectedIndex == 0 || EditDocumentCategory.SelectedItem == null);
        }

        private void BtnEditCategoryCancelClicked(object sender, RoutedEventArgs e)
        {
            ClearEditCategoryTab();
            editingCancelled = true;
            StopEditing();
        }

        private void BtnEditCategorySaveClicked(object sender, RoutedEventArgs e)
        {            
            if (editedCategory == null)
            {
                MessageBox.Show("Fehler - Keine Kategorie ausgewählt!");
                return;
            }

            var name = EditCategoryName.Text;
            var superCatString = ToSuperCatString(EditCategoryCategory.SelectedItem.ToString());
            var superCat = db.Categories.FirstOrDefault(y => y.CategoryNumber == superCatString);

            if (name == null || name == "") return;


            if (editedCategory.SuperCategory != superCat)
            {
                var childrenOfCategory = GetAllCategoryChildrenOfCategory(editedCategory);
                if (superCat != null && 
                    (childrenOfCategory.Select(x => x.CategoryId).Contains(superCat.CategoryId)
                    || superCat.CategoryId == editedCategory.CategoryId))
                {
                    var messageBoxResult = MessageBox.Show($"Die Kategorie kann nicht in eine eigene untergeordnete Katagorie verschoben werden!",
                        "Verschieben nicht möglich", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                bool hasChildren =
                    (db.Categories.Where(x => x.SuperCategory.CategoryId == editedCategory.CategoryId)
                        .Count() > 0)
                    || (db.Documents.Where(x => x.SuperCategory.CategoryId == editedCategory.CategoryId)
                        .Count() > 0);

                if (hasChildren)
                {
                    System.Windows.Style style = new System.Windows.Style();
                    style.Setters.Add(
                        new Setter(
                            Xceed.Wpf.Toolkit.MessageBox.YesButtonContentProperty,
                            CAT_HAS_CHILDREN_MOVE_BUTTON));
                    style.Setters.Add(
                        new Setter(
                            Xceed.Wpf.Toolkit.MessageBox.NoButtonContentProperty,
                            CAT_HAS_CHILDREN_MOVETOSUPER_BUTTON));
                    style.Setters.Add(
                        new Setter(
                            Xceed.Wpf.Toolkit.MessageBox.CancelButtonContentProperty,
                            CAT_HAS_CHILDREN_DELETE_BUTTON));

                    var result = Xceed.Wpf.Toolkit.MessageBox
                        .Show(MESSAGE_CAT_HAS_CHILDREN,
                            MESSAGE_CAT_HAS_CHILDREN_TITLE,
                            MessageBoxButton.YesNoCancel,
                            MessageBoxImage.Information,
                            MessageBoxResult.Yes,
                            style);

                    if (result == MessageBoxResult.Yes) // Move
                    {
                        // nothing to do -> categoryNumbers get regenerated later
                    }
                    else if (result == MessageBoxResult.No) // Move to Super
                    {
                        if (!MoveChildrenOfCategoryToSuper(editedCategory))
                        {
                            var messageBoxResult = MessageBox.Show($"Die Dokumente können nicht in die übergeordnete Katagorie verschoben werden, " +
                                $"da die Dokumente einer bestimmten Kategorie zugeordnet sein müssen!",
                                "Verschieben nicht möglich", MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }
                    }
                    else if (result == MessageBoxResult.Cancel) // Delete
                    {
                        DeleteChildrenOfCategory(editedCategory);
                    }
                    else
                    {
                        return;
                    }
                    
                }
                var oldSuperCat = editedCategory.SuperCategory;
                editedCategory.SuperCategory = superCat;
                db.SaveChanges();
                RegenerateAllCategoryNumbersInCategory(superCat);
                RegenerateAllCategoryNumbersInCategory(oldSuperCat);
            }
            if (editedCategory.CategoryName != name) editedCategory.CategoryName = name;

            try
            {
                db.SaveChanges();
            }
            catch (DbEntityValidationException exc)
            {

                foreach (var eve in exc.EntityValidationErrors)
                {
                    Console.WriteLine("Entity of type \"{0}\" in state \"{1}\" has the following validation errors:",
                        eve.Entry.Entity.GetType().Name, eve.Entry.State);
                    foreach (var ve in eve.ValidationErrors)
                    {
                        Console.WriteLine("- Property: \"{0}\", Error: \"{1}\"",
                            ve.PropertyName, ve.ErrorMessage);
                    }
                }

                throw;
            }

            ClearEditCategoryTab();
            StopEditing();
        }

        private void GenerateAllCategoryNumbersInCategory(Category cat, List<Category> ignoreCategoriesWhenGenerating)
        {
            List<Category> subCats;

            if (cat != null)
                subCats = db.Categories.Where(x => x.SuperCategory.CategoryId == cat.CategoryId).ToList();
            else
            {
                subCats = db.Categories.Where(x => x.SuperCategory == null).ToList();
            }


            foreach (var sub in subCats)
            {
                sub.SuperCategory = null;
            }

            db.SaveChanges();

            var handledCats = new List<Category>();
            var ignoredCats = new List<Category>();
            if (cat == null) ignoredCats.AddRange(subCats);
            ignoredCats.AddRange(ignoreCategoriesWhenGenerating);

            foreach (var sub in subCats)
            {
                var newCatNum = GetHighestCategoryNumberOfCategory((cat != null) ? cat.CategoryNumber : null, ignoredCats, handledCats);
                if (sub.CategoryNumber != newCatNum) sub.CategoryNumber = newCatNum;
                if (ignoredCats.Contains(sub)) handledCats.Add(sub);

                sub.SuperCategory = cat;
                db.SaveChanges();
                RegenerateAllCategoryNumbersInCategory(sub);
            }
        }

        private void RegenerateAllCategoryNumbersInCategory(Category cat, List<Category> ignoreCategoriesWhenGenerating)
        {
            ResetAllCategoryNumbersInCategory(cat);
            GenerateAllCategoryNumbersInCategory(cat, ignoreCategoriesWhenGenerating);
        }

        private void RegenerateAllCategoryNumbersInCategory(Category cat)
        {
            ResetAllCategoryNumbersInCategory(cat);
            GenerateAllCategoryNumbersInCategory(cat, new List<Category>());
        }

        private void ResetAllCategoryNumbersInCategory(Category cat)
        {
            List<Category> subCats;
            
            if (cat != null)
                subCats = db.Categories.Where(x => x.SuperCategory.CategoryId == cat.CategoryId).ToList();
            else
            {
                subCats = db.Categories.Where(x => x.SuperCategory == null).ToList();
            }

            foreach (var sub in subCats)
            {
                sub.CategoryNumber = GetHighestTempNumber();
                db.SaveChanges();

                ResetAllCategoryNumbersInCategory(sub);
            }
        }

        private List<Category> GetAllCategoryChildrenOfCategory(Category editedCategory)
        {
            var list = new List<Category>();
            var cats = db.Categories.Where(x => x.SuperCategory.CategoryId == editedCategory.CategoryId).ToList();

            foreach (var cat in cats)
            {
                list.Add(cat);
                list.AddRange(GetAllCategoryChildrenOfCategory(cat));
            }

            return list;
        }

        private void BtnEditDocumentCancelClicked(object sender, RoutedEventArgs e)
        {
            ClearEditDocumentTab();
            editingCancelled = true;
            StopEditing();
        }

        private void BtnEditDocumentSaveClicked(object sender, RoutedEventArgs e)
        {

            if (editedDocument == null)
            {
                MessageBox.Show("Fehler - Kein Dokument ausgewählt!");
                return;
            }

            var name = EditDocumentName.Text;
            var notes = EditDocumentNotes.Text;
            var date = EditDocumentDate.SelectedDate;
            if (date == null) date = DateTime.Today;
            var selectedCategory = EditDocumentCategory.SelectedItem;

            if (!CheckEnteredDocumentValid(name, date, selectedCategory)) return;


            var superCatString = ToSuperCatString(selectedCategory.ToString());
            var superCat = db.Categories.FirstOrDefault(y => y.CategoryNumber == superCatString);

            if (editedDocument.DocumentName != name) editedDocument.DocumentName = name;
            if (editedDocument.DocumentNotes != notes) editedDocument.DocumentNotes = notes;
            if (editedDocument.DocumentDate != date) editedDocument.DocumentDate = (DateTime)date;
            if (editedDocument.SuperCategory != superCat) editedDocument.SuperCategory = superCat;

            try
            {
                db.SaveChanges();
            }
            catch (DbEntityValidationException exc)
            {

                foreach (var eve in exc.EntityValidationErrors)
                {
                    Console.WriteLine("Entity of type \"{0}\" in state \"{1}\" has the following validation errors:",
                        eve.Entry.Entity.GetType().Name, eve.Entry.State);
                    foreach (var ve in eve.ValidationErrors)
                    {
                        Console.WriteLine("- Property: \"{0}\", Error: \"{1}\"",
                            ve.PropertyName, ve.ErrorMessage);
                    }
                }

                throw;
            }

            ClearEditDocumentTab();
            StopEditing();
        }


        /* Contextmenu Elements */
        public void StructureContextMenuEditClicked(object sender, RoutedEventArgs e)
        {
            var selectedItem = Structure.SelectedItem;
            if (!(selectedItem is TreeViewItem)) return;

            var selectedObject = (selectedItem as TreeViewItem).Header;

            if (selectedObject is Category)
            {
                StartEditingCategory((selectedObject as Category).CategoryId, Structure);
            }
            else if (selectedObject is Document)
            {
                StartEditingDocument((selectedObject as Document).DocumentId, Structure);

            }
        }

        public void StructureContextMenuDeleteClicked(object sender, RoutedEventArgs e)
        {


            var selectedItem = Structure.SelectedItem;
            if (!(selectedItem is TreeViewItem)) return;

            var selectedObject = (selectedItem as TreeViewItem).Header;

            if (selectedObject is Category)
            {
                var cat = selectedObject as Category;
                int superCatId;
                if (cat.SuperCategory != null) superCatId = cat.SuperCategory.CategoryId;
                else superCatId = -1;

                var res = MessageBox.Show($"Wollen sie das ausgewählte Element ({cat}) wirklich löschen?",
                    "Löschen", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res != MessageBoxResult.Yes) return;


                bool hasChildren =
                    (db.Categories.Where(x => x.SuperCategory.CategoryId == cat.CategoryId)
                        .Count() > 0)
                    || (db.Documents.Where(x => x.SuperCategory.CategoryId == cat.CategoryId)
                        .Count() > 0);

                if (hasChildren)
                {

                    System.Windows.Style style = new System.Windows.Style();
                    style.Setters.Add(
                        new Setter(
                            Xceed.Wpf.Toolkit.MessageBox.YesButtonContentProperty,
                            CAT_HAS_CHILDREN_MOVETOSUPER_BUTTON));
                    style.Setters.Add(
                        new Setter(
                            Xceed.Wpf.Toolkit.MessageBox.NoButtonContentProperty,
                            CAT_HAS_CHILDREN_DELETE_BUTTON));
                    var result = Xceed.Wpf.Toolkit.MessageBox
                        .Show(MESSAGE_CAT_HAS_CHILDREN_DELETE,
                            MESSAGE_CAT_HAS_CHILDREN_TITLE,
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information,
                            MessageBoxResult.Cancel,
                            style);


                    List<Category> subCats;
                    if (cat != null)
                        subCats = db.Categories.Where(x => x.SuperCategory != null && x.SuperCategory.CategoryId == cat.CategoryId).ToList();
                    else
                        subCats = db.Categories.Where(x => x.SuperCategory == null).ToList();

                    if (result == MessageBoxResult.No) // Delete
                    {
                        DeleteChildrenOfCategory(cat);

                    }
                    else if (result == MessageBoxResult.Yes) // Move to Super
                    {

                        if (!MoveChildrenOfCategoryToSuper(cat))
                        {
                            var messageBoxResult = MessageBox.Show($"Die Dokumente können nicht in die übergeordnete Katagorie verschoben werden, " +
                                $"da die Dokumente einer bestimmten Kategorie zugeordnet sein müssen!",
                                "Verschieben nicht möglich", MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }
                    }
                    else
                    {
                        return;
                    }
                }

                var oldSuperCat = cat.SuperCategory;
                db.Categories.Remove(cat);
                db.SaveChanges();

                RegenerateAllCategoryNumbersInCategory(oldSuperCat);

                ReloadForCategoryChange();
                UnfoldIdInTreeView(superCatId, false);

            }
            else if (selectedObject is Document)
            {
                var doc = (selectedObject as Document);
                var superCat = doc.SuperCategory.CategoryId;

                var displayDoc = new DisplayDocument
                {
                    CategoryNumber = doc.SuperCategory.CategoryNumber,
                    DocumentName = doc.DocumentName
                };
                var res = MessageBox.Show($"Wollen sie das ausgewählte Element ({displayDoc}) wirklich löschen?",
                    "Löschen", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res != MessageBoxResult.Yes) return;

                if (DeleteDocumentById(doc.DocumentId) >= 0)
                {
                    ReloadForDocumentChange();
                    UnfoldIdInTreeView(superCat, false);
                }
            }
        }

        private bool MoveChildrenOfCategoryToSuper(Category cat)
        {
            var subCats = db.Categories.Where(x => x.SuperCategory.CategoryId == cat.CategoryId).ToList();
            var subDocs = db.Documents.Where(x => x.SuperCategory.CategoryId == cat.CategoryId).ToList();
            var super = cat.SuperCategory;

            if (super == null && subDocs.Count() > 0) return false;

            foreach (var sub in subCats)
            {
                sub.SuperCategory = super;
            }

            foreach (var sub in subDocs)
            {
                sub.SuperCategory = super;
            }
            db.SaveChanges();

            return true;
        }

        private void DeleteChildrenOfCategory(Category cat)
        {
            var subCats = db.Categories.Where(x => x.SuperCategory.CategoryId == cat.CategoryId).ToList();
            var subDocs = db.Documents.Where(x => x.SuperCategory.CategoryId == cat.CategoryId).ToList();

            foreach (var sub in subCats)
            {
                DeleteChildrenOfCategory(sub);
                db.Categories.Remove(sub);
            }

            foreach (var sub in subDocs)
            {
                db.Documents.Remove(sub);
            }

            db.SaveChanges();

        }

        public void SearchContextMenuEditClicked(object sender, RoutedEventArgs e)
        {
            var doc = GetSelectedDisplayDoc();
            if (doc != null)
            {
                StartEditingDocument(doc.DocumentId, Search);
            }
        }

        public void SearchContextMenuDeleteClicked(object sender, RoutedEventArgs e)
        {
            var doc = GetSelectedDisplayDoc();
            if (doc != null)
            {
                var res = MessageBox.Show($"Wollen sie das ausgewählte Element ({doc}) wirklich löschen?",
                    "Löschen", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res != MessageBoxResult.Yes) return;

                if (DeleteDocumentById(doc.DocumentId) >= 0)
                {
                    ReloadForDocumentChange();
                }
            }
        }

        public void SearchContextMenuStructureClicked(object sender, RoutedEventArgs e)
        {
            var doc = GetSelectedDisplayDoc();
            if (doc != null)
            {
                UnfoldDocAndOpenStructure(doc.DocumentId);
            }
        }

        private DisplayDocument GetSelectedDisplayDoc()
        {
            var doc = SearchResults.SelectedItem as DisplayDocument;
            if (doc != null)
            {
                return doc;
            }
            else
            {
                MessageBox.Show("Bitte wählen sie zuerst ein Suchergebnis aus.",
                    "Kein Dokument ausgewählt", MessageBoxButton.OK, MessageBoxImage.Information);
                return null;
            }
        }

        private int DeleteDocumentById(int docId)
        {
            var doc = db.Documents.FirstOrDefault(x => x.DocumentId == docId);
            if (doc != null)
            {
                var removed = db.Documents.Remove(doc);
                db.SaveChanges();
                return removed.DocumentId;
            }
            else
            {
                return -1;
            }

        }



        private void seedDbFromCSV()
        {

            if (db.Categories.Count() > 0) return;

            if (!File.Exists(SEED_FILE_PATH)) return;

            var splitLines = File.ReadAllLines(SEED_FILE_PATH).Skip(3).Select(x => x.Split(';')).ToList();
            var cats = splitLines.Where(x => x[0].ToUpper() == bool.TrueString.ToUpper()).ToList();
            var docs = splitLines.Where(x => x[0].ToUpper() == bool.FalseString.ToUpper()).ToList();

            foreach(var line in cats)
            {
                string name = line[2];
                string number = line[1];
                string superNumber = line[3];
                Category c = new Category
                {
                    CategoryName = name,
                    CategoryNumber = number,
                    SuperCategory = db.Categories.FirstOrDefault(x => x.CategoryNumber == superNumber)
                };
                db.Categories.Add(c);
                db.SaveChanges();
            }

            foreach(var line in docs)
            {
                string name = line[1];
                string notes = line[2];
                string date = line[3];
                string superNumber = line[4];
                var temp = DateTime.Parse(date);
                var temp2 = db.Categories.FirstOrDefault(x => x.CategoryNumber == superNumber);
                Document d = new Document
                {
                    DocumentName = name,
                    DocumentNotes = notes,
                    DocumentDate = DateTime.Parse(date),
                    SuperCategory = db.Categories.First(x => x.CategoryNumber == superNumber)
                };
                db.Documents.Add(d);
            }
            db.SaveChanges();
        }

    }
}
