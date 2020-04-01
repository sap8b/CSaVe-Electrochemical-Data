using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace CSaVe_Electrochemical_Data
{
    /// <summary>
    /// Interaction logic for MessageWindowDialog.xaml
    /// </summary>
    public partial class MessageWindowDialog : Window
    {
        public int ProgressValue
        {
            set
            {
                progress.Value = value;
            }
        }

        public MessageWindowDialog()
        {
            InitializeComponent();
        }
    }
}
