using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Reflection;

namespace IMS_client
{
    /// <summary>
    /// Interaction logic for Address_Book_window.xaml
    /// </summary>
    public partial class AddressBookWindow : Window
    {
        AddressBook _addressBook;
        Contact new_contact;

        public AddressBookWindow(AddressBook passedInAddressBook)
        {
            InitializeComponent();
            new_contact = new Contact();
            _addressBook = passedInAddressBook;
            SizeToContent = SizeToContent.WidthAndHeight;
            PropertyInfo[] properties = typeof(Contact).GetProperties();

            CreateAddContactTab(properties);
            if (_addressBook != null)
            {
                CreateDisplayContactTab(properties);
            }
        }

        private void CreateAddContactTab(IEnumerable<PropertyInfo> properties)
        {
            Grid addGrid = Address_book_add_grid;
            ColumnDefinition coldef = new ColumnDefinition();
            addGrid.ColumnDefinitions.Add(coldef);
            coldef = new ColumnDefinition();
            addGrid.ColumnDefinitions.Add(coldef);

            int counter = 0;
            foreach (PropertyInfo propInfo in properties)
            {
                Create_Lbl_Txt_Row(new_contact, counter, ref addGrid, propInfo.Name);
                counter++;
            }
        }

        private void CreateDisplayContactTab(IEnumerable<PropertyInfo> properties)
        {
            ComboBox contactListBox = Address_book_combobox;
            contactListBox.ItemsSource = _addressBook.Entries;

            Grid contactInfoGrid = Address_book_grid;
            ColumnDefinition coldef = new ColumnDefinition();
            contactInfoGrid.ColumnDefinitions.Add(coldef);
            coldef.Width = GridLength.Auto;
            coldef = new ColumnDefinition {Width = GridLength.Auto};
            contactInfoGrid.ColumnDefinitions.Add(coldef);

            int counter = 0;
            foreach (PropertyInfo propInfo in properties)
            {
                Create_Lbl_Txt_Row(_addressBook.Entries[0], counter, ref contactInfoGrid, propInfo.Name);
                counter++;
            }
            contactListBox.IsTextSearchEnabled = true;
            contactListBox.SelectionChanged += ContactListBoxSelectionChanged;

        }

        private void Create_Lbl_Txt_Row(Contact bindingSource, int rowNumber, ref Grid gridToAddTo, string propInfoName)
        {
            RowDefinition rowdef = new RowDefinition();
            gridToAddTo.RowDefinitions.Add(rowdef);

            TextBox textbox = new TextBox {Tag = propInfoName, MaxHeight = 30, MaxWidth = 200, MinWidth = 100};

            Binding myBinding = new Binding("WidthProperty") {Source = gridToAddTo.ColumnDefinitions[1].Width};
            textbox.SetBinding(WidthProperty,myBinding);

            textbox.HorizontalContentAlignment = HorizontalAlignment.Center;
            textbox.HorizontalAlignment = HorizontalAlignment.Stretch;
            textbox.VerticalContentAlignment = VerticalAlignment.Center;
            textbox.VerticalAlignment = VerticalAlignment.Stretch;
            textbox.Margin = new Thickness(2);


            myBinding = new Binding(propInfoName) {Source = bindingSource};
            textbox.SetBinding(TextBox.TextProperty, myBinding);


            TextBlock name = new TextBlock
                                 {
                                     Text = propInfoName,
                                     Margin = new Thickness(2),
                                     HorizontalAlignment = HorizontalAlignment.Right,
                                     VerticalAlignment = VerticalAlignment.Center
                                 };

            gridToAddTo.Children.Add(textbox);
            gridToAddTo.Children.Add(name);

            Grid.SetColumn(textbox, 1);
            Grid.SetRow(textbox, rowNumber);

            Grid.SetColumn(name, 0);
            Grid.SetRow(name, rowNumber);
        }

        private void ContactListBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox comboBox = sender as ComboBox;
            AddressBookWindow myAddressBookWindow = TryFindParent<AddressBookWindow>(comboBox);
            Grid contactInfoGrid = myAddressBookWindow.Address_book_grid;
            int index = 0;

            if (comboBox != null && comboBox.SelectedItem != null)
            {
                foreach (Contact contact in _addressBook.Entries)
                {
                    if (contact.Name == comboBox.SelectedItem.ToString())
                    {
                        index = _addressBook.Entries.IndexOf(contact);
                    }

                }

                foreach (TextBox textBox in contactInfoGrid.Children.OfType<TextBox>())
                {
                    Binding myBinding = new Binding(textBox.Tag.ToString()) {Source = _addressBook.Entries[index]};
                    textBox.SetBinding(TextBox.TextProperty, myBinding);
                }
            }
        }

        private static T TryFindParent<T>(DependencyObject child) where T : DependencyObject
        {
            //get parent item
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);

            //we've reached the end of the tree
            if (parentObject == null) return null;

            //check if the parent matches the type we're looking for
            T parent = parentObject as T;
            if (parent != null)
            {
                return parent;
            }
            //use recursion to proceed with next level
            return TryFindParent<T>(parentObject);
        }

        private void AddContactClick(object sender, RoutedEventArgs e)
        {
            if (_addressBook == null)
            {
                _addressBook = new AddressBook();
            }
            if (new_contact.Name != "")
            {
                _addressBook.Entries.Add(new Contact(new_contact));
                ComboBox contactListBox = Address_book_combobox;
                contactListBox.Items.Refresh();
                MessageBox.Show("Contact Added");
            }
            else
            {
                MessageBox.Show("Contact must have at least a name");
            }
        }

        private void RemoveContactClick(object sender, RoutedEventArgs e)
        {

            AddressBookWindow myAddressBookWindow = TryFindParent<AddressBookWindow>(sender as Button);
            ComboBox comboBox = myAddressBookWindow.Address_book_combobox;

            int index = 0;
            foreach (Contact contact in _addressBook.Entries)
            {
                if (contact.Name == comboBox.SelectedItem.ToString())
                {
                    index = _addressBook.Entries.IndexOf(contact);
                }
            }
            _addressBook.Entries.RemoveAt(index);
            comboBox.Items.Refresh();

        }
    }
}

