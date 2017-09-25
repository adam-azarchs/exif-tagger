using System;
using System.Collections.Generic;
using System.Globalization;
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

namespace PhotoTagger {
    /// <summary>
    /// Interaction logic for PhotoListItem.xaml
    /// </summary>
    public partial class PhotoListItem : UserControl {
        public PhotoListItem() {
            InitializeComponent();
        }

        public static readonly DependencyProperty PhotoProperty =
            DependencyProperty.Register(
                "Photo", typeof(Photo), typeof(PhotoListItem));
        public Photo Photo {
            get {
                return (Photo)GetValue(PhotoProperty);
            }
            set {
                SetValue(PhotoProperty, value);
            }
        }
    }
}
