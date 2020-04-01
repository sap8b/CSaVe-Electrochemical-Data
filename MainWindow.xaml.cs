using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CSaVe_Electrochemical_Data
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private FolderBrowserDialog SelectFolderForDTAFiles, SelectFolderForCSVFiles;
        private DirectoryInfo dataFolder;

        private List<FileInfo> list_of_DTA_files;
        private List<string> list_of_folders_in_DTA_directory; // = new List<string>();
        private List<string> list_of_folders_in_CSV_directory; // = new List<string>();

        private string[] ColumnHeadings;
        private string[] Units;

        // create a background worker
        private BackgroundWorker worker;

        //Message window that displays the progress bar
        MessageWindowDialog mwd;

        public MainWindow()
        {
            InitializeComponent();
        }
        /// <summary>
        /// This method searches through the provided folder and subfolders or just through the provided folder to find all DTA files and then uses the Background Worker to call
        /// the file conversion method on each file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ConvertFiles_Click(object sender, RoutedEventArgs e)
        {
            mwd = new MessageWindowDialog();
            StatusBox.Text = "Conversions not yet finsished.";

            //Initialize the dispatcher
            System.Windows.Threading.Dispatcher mwDispatcher = mwd.Dispatcher;

            //Initialize the background worker and support progress reporting
            worker = new BackgroundWorker
            {
                WorkerReportsProgress = true
            };


            // Check to see if both the DTA folders list has at least one entry in it and the CSV folders list has at least one entry in it
            if (list_of_folders_in_DTA_directory.Count > 0 && list_of_folders_in_CSV_directory.Count > 0)
            {
                worker.DoWork += delegate (object s, DoWorkEventArgs args)
                {
                    int maxNumFiles = 0;
                    int k = 0;

                    // Iterate through each folder name in the DTA folders list
                    foreach (string entry in list_of_folders_in_DTA_directory)
                    {
                        // Initialize each folder name as a directoryinfo object
                        DirectoryInfo dataFolder = new DirectoryInfo(entry);

                        // Search the directoryinfo folder for all dta files and add them to a FIleInfo array
                        FileInfo[] temp_list_files = dataFolder.GetFiles("*.dta");

                        // If there is at least one dta file in the folder, iterate through the array of fileinfo and add the names to a list of DTA names
                        if (temp_list_files.Length > 0)
                        {
                            list_of_DTA_files = new List<FileInfo>();
                            foreach (FileInfo lfile in temp_list_files)
                            {
                                list_of_DTA_files.Add(lfile);
                            }

                            // Create and initialize a DirectoryInfo object for the matching CSV folder name from the CSV folder list
                            DirectoryInfo copyFolder = new DirectoryInfo(list_of_folders_in_CSV_directory[k]);

                            // For each FIleInfo object in the list of dta files, create a StreamReader object, create the new csv filenmae, and pass these entries
                            // to the Conversion routine
                            maxNumFiles = list_of_DTA_files.Count;
                            int ii = 0;
                            foreach (FileInfo file in list_of_DTA_files)
                            {
                                StreamReader sR = new StreamReader(file.OpenRead());

                                string iname = file.Name;
                                string[] split_filename = iname.Split('.', StringSplitOptions.RemoveEmptyEntries);

                                string new_filename_base = split_filename[0].ToUpper();
                                string suffix = ".csv";

                                int result = ConvertGamryEISDatafileToCSVFile(copyFolder.FullName, new_filename_base, suffix, sR);

                                // Create and initialize a delegate to accept the progress bar update method
                                UpdateProgressDelegate update = new UpdateProgressDelegate(UpdateProgressBar);

                                // Pass the delegate and the value to the dispatcher
                                mwDispatcher.BeginInvoke(update, Convert.ToInt32(ii / (decimal)maxNumFiles * 100), maxNumFiles);
                                ii += 1;

                            }
                        }

                        k += 1;
                    }


                    // Background worker method for reporting progress
                    worker.ProgressChanged += delegate (object s, ProgressChangedEventArgs args)
                    {
                        int percentage = args.ProgressPercentage;

                    };

                };

                // Background worker method to report task complete
                worker.RunWorkerCompleted += delegate (object s, RunWorkerCompletedEventArgs args)
                {
                    StatusBox.Text = "Conversion Complete";
                    mwd.Close();
                };

                worker.RunWorkerAsync();
                mwd.ShowDialog();

            }
        }

        /// <summary>
        /// Delegate used for the method to update the Message Window's progress bar
        /// </summary>
        /// <param name="percentage"></param>
        /// <param name="recordCount"></param>
        public delegate void UpdateProgressDelegate(int percentage, int recordCount);

        /// <summary>
        /// Method for updating the Message Window's progress bar
        /// </summary>
        /// <param name="percentage"></param>
        /// <param name="recordCount"></param>        
        public void UpdateProgressBar(int percentage, int recordCount)
        {
            mwd.ProgressValue = percentage;
        }

        /// <summary>
        /// This method provides the folder browser dialog to permit designating the directory in which to store the converted datafiles
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SelectOutputLocationButton_Click(object sender, RoutedEventArgs e)
        {
            list_of_folders_in_CSV_directory = new List<string>();
            SelectFolderForCSVFiles = new FolderBrowserDialog
            {
                RootFolder = Environment.SpecialFolder.MyComputer,
                UseDescriptionForTitle = true,
                Description = "Pick the parent directory to create the new CSV files in"
            };

            SelectFolderForCSVFiles.ShowDialog();
            string csv_directory = SelectFolderForCSVFiles.SelectedPath;

            if (csv_directory != null && csv_directory != "")
            {
                OutputFolder.Text = csv_directory;

                // This conditional statement checks the length of the dta folder list.
                // If the count > 0 then it creates copies of the folders
                if (list_of_folders_in_DTA_directory.Count > 0)
                {
                    int i = 0;
                    foreach (string centry in list_of_folders_in_DTA_directory)
                    {
                        DirectoryInfo subFolder = new DirectoryInfo(centry);
                        string new_name = System.IO.Path.Combine(csv_directory, subFolder.Name);

                        if (!Directory.Exists(new_name))
                        {
                            Directory.CreateDirectory(new_name);
                            list_of_folders_in_CSV_directory.Add(new_name);
                            i += 1;
                        }
                        else
                        {
                            list_of_folders_in_CSV_directory.Add(new_name);
                            i += 1;
                        }
                    }
                    ConvertFiles.IsEnabled = true;
                }
                // If the count == 0, then it creates only a single folder that matches the folder name in the dta folder list
                else
                {
                    string new_name = System.IO.Path.Combine(csv_directory, dataFolder.Name);
                    list_of_folders_in_CSV_directory.Add(new_name);

                    if (!Directory.Exists(new_name))
                    {
                        Directory.CreateDirectory(new_name);
                    }
                    if (list_of_DTA_files.Count > 0)
                    {
                        ConvertFiles.IsEnabled = true;
                    }
                    else
                    {
                        System.Windows.MessageBox.Show("There do not appear to be any DTA files in the selected directory.");
                    }
                }

            }
        }

        /// <summary>
        /// This method provides the folder browser dialog to designate the base folder where the dta files are located
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LoadDataButton_Click(object sender, RoutedEventArgs e)
        {
            // Iniitialize critical settings
            DataFilesTextBox.Text = "Datafiles";
            StatusBox.Text = "Status";
            list_of_folders_in_DTA_directory = new List<string>();

            SelectFolderForDTAFiles = new FolderBrowserDialog
            {
                RootFolder = Environment.SpecialFolder.MyComputer,
                UseDescriptionForTitle = true,
                Description = "Pick the parent directory for the folders containing DTA datafiles."
            };

            SelectFolderForDTAFiles.ShowDialog();
            string base_directory = SelectFolderForDTAFiles.SelectedPath;

            if (base_directory != null && base_directory != "")
            {
                ParentFolder.Text = base_directory;

                DirectoryInfo directory = new DirectoryInfo(base_directory);
                string[] temp_directory_array = Directory.GetDirectories(directory.FullName);

                // This conditional statement checks to see if multiple folders are present in the selected data folder
                // If there are multiple folders then they are added to the dta folder list
                if (temp_directory_array.Length > 0)
                {
                    foreach (string t_directory in temp_directory_array)
                    {
                        list_of_folders_in_DTA_directory.Add(t_directory);
                        DataFilesTextBox.Text += t_directory + Environment.NewLine;
                    }
                    SelectOutputLocationButton.IsEnabled = true;
                }
                // if there are not multiple folders then the base folder is assumed to contain all of the dta files.
                // The base folder is added to the dta folder list
                else
                {
                    dataFolder = new DirectoryInfo(base_directory);
                    list_of_folders_in_DTA_directory.Add(base_directory);

                    // The base folder is searched for dta files
                    FileInfo[] temp_list_files = dataFolder.GetFiles("*.dta");

                    // If any are found, they are added to the list of dta files
                    if (temp_list_files.Length > 0)
                    {
                        list_of_DTA_files = new List<FileInfo>();
                        ; foreach (FileInfo t_directory in temp_list_files)
                        {
                            list_of_DTA_files.Add(t_directory);
                            DataFilesTextBox.Text += t_directory + Environment.NewLine;
                        }
                        SelectOutputLocationButton.IsEnabled = true;
                    }
                    // If none are found, then an error message is displayed
                    else
                    {
                        System.Windows.MessageBox.Show("There do not appear to be any DTA files in this directory.");
                    }
                }
            }
            // If no data directory is chosen, an error message is displayed
            else
            {
                System.Windows.MessageBox.Show("Please select a directory.");
            }
        }

        /// <summary>
        /// This method extracts the experimental data from the dta file and copies it to the csv file
        /// </summary>
        /// <param name="copy_filename"></param>
        /// <param name="sR"></param>
        /// <returns></returns>
        private int ConvertGamryEISDatafileToCSVFile(string copy_folder, string copy_filename_base, string suffix, StreamReader sR)
        {
            int result = 0;

            _ = sR.ReadLine();
            string line1 = sR.ReadLine();

            string[] line1_split = line1.Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);

            if (line1_split.Length > 0)
            {
                switch (line1_split[1].ToUpper())
                {
                    case "EISPOT":
                        result = 1;
                        string copy_filename = System.IO.Path.Combine(copy_folder, copy_filename_base + suffix);
                        FileStream fS = new FileStream(copy_filename, FileMode.Create);
                        StreamWriter sW = new StreamWriter(fS);

                        string line2_and_greater;
                        bool keep_one_line = false;
                        bool keep_all_lines = false;

                        int line_count = 0;
                        while ((line2_and_greater = sR.ReadLine()) != null)
                        {
                            string[] line_array = line2_and_greater.Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);

                            if (keep_one_line == false && keep_all_lines == true)
                            {
                                int point_check = Convert.ToInt32(line_array[0]);

                                if (line_count != point_check)
                                {
                                    string the_line2 = null;
                                    for (int i = 0; i <= line_array.Length - 2; i++)
                                    {
                                        the_line2 += line_array[i] + ",";
                                    }
                                    the_line2 += line_array[^1];

                                    sW.WriteLine(the_line2);

                                    line_count += 1;
                                    //Console.WriteLine("Help!");
                                }
                                string the_line = null;
                                for (int i = 0; i <= line_array.Length - 2; i++)
                                {
                                    the_line += line_array[i] + ",";
                                }
                                the_line += line_array[^1];

                                sW.WriteLine(the_line);

                                line_count += 1;
                            }

                            if (line_array.Length > 0)
                            {
                                if (line_array[0] == "ZCURVE" && keep_one_line == false)
                                {
                                    keep_one_line = true;
                                }
                                if (keep_one_line == true)
                                {
                                    string next_line = sR.ReadLine();
                                    ColumnHeadings = new string[11];
                                    Array.Copy(next_line.Split('\t'), 1, ColumnHeadings, 0, 11);

                                    string the_line = null;
                                    for (int i = 0; i <= ColumnHeadings.Length - 2; i++)
                                    {
                                        the_line += ColumnHeadings[i] + ",";
                                    }
                                    the_line += ColumnHeadings[ColumnHeadings.Length - 1];

                                    sW.WriteLine(the_line);


                                    next_line = sR.ReadLine();
                                    Units = new string[11];
                                    Array.Copy(next_line.Split('\t'), 1, Units, 0, 11);
                                    keep_one_line = false;
                                    keep_all_lines = true;
                                }
                            }
                        }
                        sW.Close();
                        fS.Close();
                        break;

                    case "POTENTIODYNAMIC":
                        result = 1;
                        string eoc_copy_filename = System.IO.Path.Combine(copy_folder, copy_filename_base + "e_oc" + suffix);
                        string pd_copy_filename = System.IO.Path.Combine(copy_folder, copy_filename_base + "pd" + suffix);

                        FileStream fS1 = new FileStream(eoc_copy_filename, FileMode.Create);
                        FileStream fS2 = new FileStream(pd_copy_filename, FileMode.Create);
                        StreamWriter sW1 = new StreamWriter(fS1);
                        StreamWriter sW2 = new StreamWriter(fS2);

                        string eoc_readline;
                        bool keep_one_line2 = false;
                        bool keep_one_line3 = false;
                        bool keep_all_lines2 = false;
                        bool keep_all_lines3 = false;

                        int line_count2 = 0;

                        while ((eoc_readline = sR.ReadLine()) != null)
                        {
                            string[] line_array = eoc_readline.Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);

                            if (line_array.Length > 0)
                            {
                                if (line_array[0] == "EOC" && keep_all_lines2 == true)
                                {
                                    keep_all_lines2 = false;
                                    keep_one_line2 = false;
                                }

                                if (line_array[0] == "CURVE" && keep_one_line3 == false)
                                {
                                    keep_one_line3 = true;
                                }

                            }

                            if (keep_one_line2 == false && keep_all_lines2 == true)
                            {
                                int point_check = Convert.ToInt32(line_array[0]);

                                if (line_count2 != point_check)
                                {
                                    string the_line2 = null;
                                    for (int i = 0; i <= line_array.Length - 2; i++)
                                    {
                                        the_line2 += line_array[i] + ",";
                                    }
                                    the_line2 += line_array[^1];

                                    sW1.WriteLine(the_line2);

                                    line_count2 += 1;
                                }

                                string the_line = null;
                                for (int i = 0; i <= line_array.Length - 2; i++)
                                {
                                    the_line += line_array[i] + ",";
                                }
                                the_line += line_array[^1];

                                sW1.WriteLine(the_line);

                                line_count2 += 1;
                            }

                            if (keep_one_line3 == false && keep_all_lines3 == true)
                            {
                                int point_check = Convert.ToInt32(line_array[0]);

                                if (line_count2 != point_check)
                                {
                                    string the_line2 = null;
                                    for (int i = 0; i <= line_array.Length - 2; i++)
                                    {
                                        the_line2 += line_array[i] + ",";
                                    }
                                    the_line2 += line_array[^1];

                                    sW2.WriteLine(the_line2);

                                    line_count2 += 1;
                                }

                                string the_line = null;
                                for (int i = 0; i <= line_array.Length - 2; i++)
                                {
                                    the_line += line_array[i] + ",";
                                }
                                the_line += line_array[^1];

                                sW2.WriteLine(the_line);

                                line_count2 += 1;
                            }

                            if (line_array.Length > 0)
                            {
                                if (line_array[0] == "OCVCURVE" && keep_one_line2 == false)
                                {
                                    keep_one_line2 = true;
                                }

                                if (keep_one_line2 == true)
                                {
                                    string next_line = sR.ReadLine();
                                    string[] nline_split = next_line.Split('\t');

                                    ColumnHeadings = new string[nline_split.Length];

                                    Array.Copy(nline_split, 1, ColumnHeadings, 0, nline_split.Length - 1);

                                    string the_line = null;
                                    for (int i = 0; i <= ColumnHeadings.Length - 2; i++)
                                    {
                                        the_line += ColumnHeadings[i] + ",";
                                    }
                                    the_line += ColumnHeadings[^1];

                                    sW1.WriteLine(the_line);


                                    next_line = sR.ReadLine();
                                    nline_split = next_line.Split('\t');
                                    Units = new string[nline_split.Length];

                                    Array.Copy(next_line.Split('\t'), 1, Units, 0, nline_split.Length - 1);
                                    keep_one_line2 = false;
                                    keep_all_lines2 = true;
                                }



                                if (keep_one_line3 == true)
                                {
                                    string next_line = sR.ReadLine();
                                    string[] nline_split = next_line.Split('\t');

                                    ColumnHeadings = new string[nline_split.Length];
                                    Array.Copy(next_line.Split('\t'), 1, ColumnHeadings, 0, nline_split.Length - 1);

                                    string the_line = null;
                                    for (int i = 0; i <= ColumnHeadings.Length - 2; i++)
                                    {
                                        the_line += ColumnHeadings[i] + ",";
                                    }
                                    the_line += ColumnHeadings[^1];

                                    sW2.WriteLine(the_line);


                                    next_line = sR.ReadLine();
                                    nline_split = next_line.Split('\t');
                                    Units = new string[nline_split.Length];
                                    Array.Copy(next_line.Split('\t'), 1, Units, 0, nline_split.Length - 1);
                                    keep_one_line3 = false;
                                    keep_all_lines3 = true;
                                }
                            }
                        }

                        sW2.Close();
                        sW1.Close();
                        fS2.Close();
                        fS1.Close();


                        break;
                }



            }



            return result;
        }
    }
}
