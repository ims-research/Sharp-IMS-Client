using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Reflection;

namespace IMS_client
{
    /// <summary>
    /// Interaction logic for Address_Book_window.xaml
    /// </summary>
    public partial class Address_Book_window : Window
    {
        Address_Book address_book;
        Contact new_contact;

        public Address_Book_window(Address_Book passed_in_address_book)
        {
            InitializeComponent();
            this.new_contact = new Contact();
            this.address_book = passed_in_address_book;
            this.SizeToContent = SizeToContent.WidthAndHeight;
            PropertyInfo[] properties = null;
            properties = typeof(Contact).GetProperties();

            Create_Add_Contact_Tab(properties);
            if (address_book != null)
            {
                Create_Display_Contact_Tab(properties);
            }
        }

        private void Create_Add_Contact_Tab(PropertyInfo[] properties)
        {
            Grid add_grid = this.Address_book_add_grid;
            ColumnDefinition coldef = new ColumnDefinition();
            add_grid.ColumnDefinitions.Add(coldef);
            coldef = new ColumnDefinition();
            add_grid.ColumnDefinitions.Add(coldef);

            int counter = 0;
            foreach (PropertyInfo prop_info in properties)
            {
                Create_Lbl_Txt_Row(new_contact, counter, ref add_grid, prop_info.Name);
                counter++;
            }
        }

        private void Create_Display_Contact_Tab(PropertyInfo[] properties)
        {
            ComboBox contact_list_box = this.Address_book_combobox;
            contact_list_box.ItemsSource = address_book.entries;

            Grid contact_info_grid = this.Address_book_grid;
            ColumnDefinition coldef = new ColumnDefinition();
            contact_info_grid.ColumnDefinitions.Add(coldef);
            coldef.Width = GridLength.Auto;
            coldef = new ColumnDefinition();
            coldef.Width = GridLength.Auto;
            contact_info_grid.ColumnDefinitions.Add(coldef);

            int counter = 0;
            foreach (PropertyInfo prop_info in properties)
            {
                Create_Lbl_Txt_Row(address_book.entries[0], counter, ref contact_info_grid, prop_info.Name);
                counter++;
            }
            contact_list_box.IsTextSearchEnabled = true;
            contact_list_box.SelectionChanged += new SelectionChangedEventHandler(contact_list_box_SelectionChanged);

        }

        private void Create_Lbl_Txt_Row(Contact binding_source, int row_number, ref Grid grid_to_add_to, string prop_info_name)
        {
            RowDefinition rowdef = new RowDefinition();
            grid_to_add_to.RowDefinitions.Add(rowdef);

            TextBox textbox = new TextBox();
            textbox.Tag = prop_info_name;
            textbox.MaxHeight = 30; 
            textbox.MaxWidth = 200;
            textbox.MinWidth = 100;
            Binding myBinding = null;
            myBinding = new Binding("WidthProperty");
            myBinding.Source = grid_to_add_to.ColumnDefinitions[1].Width;
            textbox.SetBinding(TextBox.WidthProperty,myBinding);

            textbox.HorizontalContentAlignment = HorizontalAlignment.Center;
            textbox.HorizontalAlignment = HorizontalAlignment.Stretch;
            textbox.VerticalContentAlignment = VerticalAlignment.Center;
            textbox.VerticalAlignment = VerticalAlignment.Stretch;
            textbox.Margin = new Thickness(2);

            
            myBinding = new Binding(prop_info_name);
            myBinding.Source = binding_source;
            textbox.SetBinding(TextBox.TextProperty, myBinding);


            TextBlock name = new TextBlock();
            name.Text = prop_info_name;
            name.Margin = new Thickness(2);
            name.HorizontalAlignment = HorizontalAlignment.Right;
            name.VerticalAlignment = VerticalAlignment.Center;

            grid_to_add_to.Children.Add(textbox);
            grid_to_add_to.Children.Add(name);

            Grid.SetColumn(textbox, 1);
            Grid.SetRow(textbox, row_number);

            Grid.SetColumn(name, 0);
            Grid.SetRow(name, row_number);
        }

        private void contact_list_box_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox combo_box = sender as ComboBox;
            Address_Book_window my_address_book_window = TryFindParent<Address_Book_window>(combo_box);
            Grid contact_info_grid = my_address_book_window.Address_book_grid;
            int index = 0;

            if (combo_box.SelectedItem != null)
            {
                foreach (Contact contact in address_book.entries)
                {
                    if (contact.Name == combo_box.SelectedItem.ToString())
                    {
                        index = address_book.entries.IndexOf(contact);
                    }

                }

                foreach (TextBox text_box in contact_info_grid.Children.OfType<TextBox>())
                {
                    Binding myBinding = new Binding(text_box.Tag.ToString());
                    myBinding.Source = address_book.entries[index];
                    text_box.SetBinding(TextBox.TextProperty, myBinding);
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
            else
            {
                //use recursion to proceed with next level
                return TryFindParent<T>(parentObject);
            }
        }

        private void Add_Contact_Click(object sender, RoutedEventArgs e)
        {
            if (address_book == null)
            {
                address_book = new Address_Book();
            }
            if (new_contact.Name != "")
            {
                address_book.entries.Add(new Contact(new_contact));
                ComboBox contact_list_box = this.Address_book_combobox;
                contact_list_box.Items.Refresh();
                MessageBox.Show("Contact Added");
            }
            else
            {
                MessageBox.Show("Contact must have at least a name");
            }
        }

        private void Remove_Contact_Click(object sender, RoutedEventArgs e)
        {

            Address_Book_window my_address_book_window = TryFindParent<Address_Book_window>(sender as Button);
            ComboBox combo_box = my_address_book_window.Address_book_combobox;

            int index = 0;
            foreach (Contact contact in address_book.entries)
            {
                if (contact.Name == combo_box.SelectedItem.ToString())
                {
                    index = address_book.entries.IndexOf(contact);
                }
            }
            address_book.entries.RemoveAt(index);
            combo_box.Items.Refresh();

        }
    }
}

