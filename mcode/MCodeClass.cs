using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace mcode
{
    public class MCodeClass
    {
        TextWriter logFile;
        static private System.DateTime ToDateTime(long seconds)
        {
            System.DateTime dt1970 = new System.DateTime(1970, 1, 1);
            return dt1970.AddSeconds(seconds);
        }

        static private long ToSecondsSince1970(System.DateTime time)
        {
            System.DateTime dt1970 = new System.DateTime(1970, 1, 1);
            TimeSpan span = time - dt1970;
            return (long)span.TotalSeconds;
        }

        public class MCodeOptions
        {
            public bool ConvertHeightToMeters = false;
        } 

        public MCodeOptions Options = new MCodeOptions();

        public List<string> DasFilesWithoutWaypoints = new List<string>();

        private static void OnException(Exception exc, string msg)
        {
            System.Diagnostics.Debug.WriteLine(exc.Message);

            if( string.IsNullOrEmpty( msg ) )
                msg = exc.Message;

            throw new System.Exception(msg, exc);
        }

        public class SecondsSince1970
        {
            public long Time;
        }

        public class TimedCoordinate : SecondsSince1970
	    {
            //waypoint file has following format :
            //est lon	est lat	pixelX	pixelY	 Time	 scannerID	 Scanner_latitude	 Scanner_longitude	 GPSlock 	RasterFile	Floor
            //-90.08073121	29.95202768	1139.7	45.4	1321999454	10017	29.94988632	-90.08145905	FALSE	office_floor6.jpg	6																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																					
            //-90.08075109	29.95198913	1139.7	92.4	1321999512	10017	29.94988632	-90.08145905	FALSE	office_floor6.jpg	6																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																																					
            //\t is used as the separator
            //
            //old format (which was used with GUI version up to 2.7) was:
            //Longitude	Latitude	Seconds	ScannerId	AnyAdditionalColumns
            //41.8898909588	12.4923725508	1316180034	10017	0
            //41.8899036078	12.4925464746	1316180038	10017	1

            //0-based indexes
            public double Lon; //0
            public double Lat; //1
            //public ulong Time; //4

            public double pixelX; //2
            public double pixelY; //3
            public string ScannerID; //5
            //public double Scanner_latitude; //6
            //public double Scanner_longitude; //7
            public string GPSlock; //8
            public string RasterFile; //9
            public string Floor; //10

            public TimedCoordinate(string[] row)
            {
                try
                {
                    Lon = double.Parse(row[0]);
                    Lat = double.Parse(row[1]);
                    pixelX = double.Parse(row[2]);
                    pixelY = double.Parse(row[3]);
                    Time = long.Parse(row[4]);

                    ScannerID = row[5];
                    GPSlock = row[8];
                    RasterFile = row[9];
                    Floor = row[10];
                }
                catch (Exception exc)
                {
                    OnException(exc, "Error while processing input waypoints file. Have you choosen correct file? Check format of the file.");
                }
            }

        }

        public class DasTx : SecondsSince1970
        {
            //DasTx measurement file has following format :
            //version produced by the DAS Control v7.6.1 (v7.5.0 also works)
            //NANOSECONDS	Latitude	Longitude	TxCode	ECIO	RxLevel	RMS_SIGNAL	RMS_CORR	NORM_CORR	ScannerID	COLLECTION_ROUND	CARRIER_SIGNAL_LEVEL	CARRIER_FREQUENCY	CARRIER_BANDWIDTH	TimeInSec	STATUS_FLAGS
            //48836035135794	33.8350296020508	-84.326057434082	1	-4.8092421028957	-90.6051366262178	2126.6953125	1222.48395763167	0.574827973921004	10017	0	-85.7958945233221	1860000000	4000000	1316180036	0
            //48836043753845	33.8350296020508	-84.326057434082	10	-9.06408295889451	-92.1624676003286	2901.220703125	1021.82522598077	0.352205271691371	10017	0	-83.0983846414341	1860000000	4000000	1316180036	0

            //0-based indexes
            public double Lon;  //2
            public double Lat;  //1
            public string TxSerial; //3 
            public string EcIo; // 4
            public string RxLev; //5
            //public ulong Time; //14 In seconds since 01-Jan-1970;
            public string ScannerID; //9
            public DateTime Date; // In human readable form Date & Time
            public string CollectionRound; // 10
            public string frequency; // 12
            //new fields to output. these data are taken from waypoint records:
            public double pixelX = 0;
            public double pixelY = 0;
            public string RasterFile = "";
            public string Floor = "";
            public bool valid = false;
            public int statusFlags = 0;
            public bool IsInterpolated = false;
            public string Filename = "";

            public long nanoseconds = 0;
            public string cipher = "";

            public static bool encrypt = true;

            public DasTx(string[] row, bool encrypted)
            {
                try
                {
                    nanoseconds = long.Parse(row[0]);
                    Lat = double.Parse(row[1]);
                    Lon = double.Parse(row[2]);
                    TxSerial = row[3];                    
                    if (encrypted)
                    {
                        cipher = row[4];
                        EcIo = "";
                        RxLev = "";
                        ScannerID = row[8];
                        CollectionRound = row[9];
                        decimal tmp = decimal.Parse(row[10]); tmp = tmp / 1000000;
                        frequency = tmp.ToString();
                        Date = ToDateTime(Time);
                        Floor = "0";
                        statusFlags = int.Parse(row[13]);
                    }
                    else
                    {
                        cipher = "";
                        RxLev = row[5];
                        EcIo = row[4];
                        ScannerID = row[9];
                        CollectionRound = row[10];
                        decimal tmp = decimal.Parse(row[12]); tmp = tmp / 1000000;
                        frequency = tmp.ToString();
                        Time = long.Parse(row[14]);
                        Date = ToDateTime(Time);
                        Floor = "0";
                        statusFlags = int.Parse(row[15]);
                    }
                }
                catch (Exception exc)
                {
                    OnException(exc, "Error while processing DAS measurements file. Please check format of the file.");
                }
            }

            public bool IsGpsLocked()
            {
                const int gpsNotLockedFlag = 0x0001;
                return (statusFlags & gpsNotLockedFlag) != gpsNotLockedFlag;
            }

            public static void WriteTmpHeaderWithAdditionalFields(StreamWriter f)
            {
                try
                {
                    if(encrypt)
                        f.WriteLine("Filename\tIsInterpolated\tMeasCount\tLongitude\tUmtsAsnVersion1.0.0Latitude\tSecCode\tBroadcastCode\tCenterFreq\tDate\tTime\tNanoseconds\tScannerID\tHGT_AGL\tSignalLevels");
                    else
                        f.WriteLine("Filename\tIsInterpolated\tMeasCount\tLongitude\tUmtsAsnVersion1.0.0Latitude\tSecCode\tBroadcastCode\tCenterFreq\tDate\tTime\tNanoseconds\tScannerID\tHGT_AGL\tCPICH RSCP\tInterference");
                }
                catch (Exception exc)
                {
                    OnException(exc, "Error while writing temporary header to a file.");
                }
            }

            public static void WriteTmpHeader(StreamWriter f)
            {
                try
                {
                    if (encrypt)
                        f.WriteLine("MeasCount\tLongitude\tUmtsAsnVersion1.0.0Latitude\tSecCode\tBroadcastCode\tCenterFreq\tDate\tTime\tNanoseconds\tScannerID\tHGT_AGL\tSignalLevels");
                    else
                        f.WriteLine("MeasCount\tLongitude\tUmtsAsnVersion1.0.0Latitude\tSecCode\tBroadcastCode\tCenterFreq\tDate\tTime\tNanoseconds\tScannerID\tHGT_AGL\tCPICH RSCP\tInterference");
                }
                catch (Exception exc)
                {
                    OnException(exc, "Error while writing temporary header to a file.");
                }
            }

            public void WriteToFileTmpVersionWithAdditionalFields(StreamWriter f)
            {
                try
                {
                    if (valid)
                    {
                        f.Write(Filename);   f.Write('\t');
                        f.Write(IsInterpolated);    f.Write("\t");
                        WriteToFileTmpVersion(f);
                    }
                }
                catch (Exception exc)
                {
                    OnException(exc, "Error while writing result to a file.");
                }
            }

            public void WriteToFileTmpVersion(StreamWriter f)
            {
                try
                {
                    if (valid)
                    {
                        f.Write(CollectionRound); f.Write('\t');
                        f.Write(Lon.ToString("0.0000000")); f.Write('\t');
                        f.Write(Lat.ToString("0.0000000")); f.Write('\t');
                        f.Write("Code_" + TxSerial + "_" + (frequency.Replace('.', '_'))); f.Write('\t');
                        f.Write(TxSerial); f.Write('\t');
                        f.Write(frequency); f.Write('\t');
                        f.Write(Date.ToShortDateString()); f.Write('\t');
                        f.Write(Date.ToString("HH:mm:ss")); f.Write('\t');
                        f.Write(nanoseconds); f.Write('\t');
                        f.Write(ScannerID); f.Write('\t');
                        f.Write(Floor); f.Write('\t');
                        if (encrypt)
                        {
                            if (cipher == "")
                            {
                                signal_levels sls;
                                sls.BroadcastSignalLevel = Double.Parse(RxLev);
                                sls.Ecio = Double.Parse(EcIo);
                                f.Write(crypto.Encrypt(sls, ToSecondsSince1970(Date), nanoseconds));
                            }
                            else
                                f.Write(cipher);
                        }
                        else
                        {
                            signal_levels sls;
                            if (RxLev == "" && EcIo == "")
                            {
                                sls = crypto.Decrypt(cipher, ToSecondsSince1970(Date), nanoseconds);
                                f.Write(sls.BroadcastSignalLevel.ToString("0.000")); f.Write('\t');
                                f.Write(sls.Ecio.ToString("0.000"));
                            }
                            else
                            {
                                f.Write(RxLev); f.Write('\t');
                                f.Write(EcIo);
                            }
                        }
                       f.WriteLine();
                    }
                }
                catch (Exception exc)
                {
                    OnException(exc, "Error while writing result to a file.");
                }
            }
        }

        //expects something like : abc.txt (though really the extension can be anything)
        //returns something like : abc.dasout.txt
        public string GetOutFileName( string measurementFile )
        {
            string name = measurementFile;

            try
            {
                if (!string.IsNullOrEmpty(name))
                {
                    int idx = name.LastIndexOf('.');
                    if (idx >= 0)
                    {
                        name = name.Substring(0, idx);
                        name += ".dasout.wna";
                    }
                }
            }
            catch (Exception exc)
            {
                OnException(exc, "Cannot create output file name.");
            }

            return name;
        }

        public bool OutputReformattedMeasurements(List<string> measurementFiles)
        {
            ValidateMeasurementFiles(measurementFiles);

            foreach (string measurementFile in measurementFiles)
            {
                string outputFileName = GetOutFileName(measurementFile);
                List<DasTx> measurements = LoadDasTxMeasurement(measurementFile);

                ValidateMeasurementsUsingGps(measurements);

                if (Options.ConvertHeightToMeters)
                    ConvertHeightToMeters(measurements);
                
                WriteToFile(measurements, outputFileName);
            }
            return true;

        }

        public void ValidateMeasurementsUsingGps(List<DasTx> measurements)
        {
            for (int idx = 0; idx < measurements.Count; idx++)
            {
                if (measurements[idx].IsGpsLocked())
                    measurements[idx].valid = true;
            }
        }

        //saves each result file into the same folder where corresponding das measurement file is located
        public bool ProcessFiles(List<string> waypointFiles, List<string> measurementFiles)
        {
            DasFilesWithoutWaypoints.Clear();

            ValidateMeasurementFiles(measurementFiles);

            //System.Collections.ArrayList arrayOfWaypointsLists = LoadWaypoinsFiles(waypointFiles);

            foreach (string measurementFile in measurementFiles)
            {
                string outputFileName = GetOutFileName(measurementFile);

                List<DasTx> measurements = LoadDasTxMeasurement(measurementFile);

                RemoveOutdatedMeasurements(measurements, measurementFile);

                AdjustTimeStamps(measurements);

                var arrayOfWaypointsLists = createWaypointArrayList(measurementFile);

                if (HasAssociatedWaypointFile(measurements, waypointFiles))
                    InterpolateFile(arrayOfWaypointsLists, measurements, measurementFile);
                else
                {
                    DasFilesWithoutWaypoints.Add(measurementFile);
                    ValidateMeasurementsUsingGps(measurements);
                }
 
                WriteToFile(measurements, outputFileName);
            }
            return true;
        }

        private System.Collections.ArrayList createWaypointArrayList(string measurementFile) {
            var waypointFilename = measurementFile.Replace(".dmm", ".wpt");
            List<string> tmpWaypointFileList = new List<string>();
            tmpWaypointFileList.Add(waypointFilename);
            return LoadWaypoinsFiles(tmpWaypointFileList);
        }

        public void ValidateWaypointFiles(List<string> waypointFiles)
        {
            if (waypointFiles == null || waypointFiles.Count < 1)
                throw new Exception("Provide waypoints files names.");
        }

        private static void ValidateMeasurementFiles(List<string> measurementFiles)
        {
            if (measurementFiles == null || measurementFiles.Count < 1)
                throw new Exception("Provide measurement filenames.");
        }

        private static void RemoveOutdatedMeasurements(List<DasTx> measurements, string filename)
        {
            measurements.Sort(CompareByTime);
            const int maxOutdatedMeasurements = 3;
            int i = 0;
            while (i < measurements.Count)
            {
                var cr0 = long.Parse(measurements[0].CollectionRound);
                var cri = long.Parse(measurements[i].CollectionRound);
                if (cr0 > cri)
                {
                    if(Math.Abs(long.Parse(measurements[i - 1].CollectionRound) - cr0) > maxOutdatedMeasurements)
                        throw new Exception(filename + " looks suspect.  A large number of measurements at the beginning of the file look out of date.");
                    else
                    {
                        // Remove all the measurements in the beginning of the file that are out of date.
                        measurements.RemoveRange(0, i);
                    }
                }
                else 
                    ++i;
            }
        }
        private static void AdjustTimeStamps(List<DasTx> measurements)
        {
            measurements.Sort(CompareByCollectionRound);

            int i = 0;
            while (i < measurements.Count)
            {
                var cr = long.Parse(measurements[i].CollectionRound);
                if (cr % 2 == 0)
                {
                    int j = i;
                    double total = 0;
                    long sum = 0;
                    while (long.Parse(measurements[j].CollectionRound) - cr < 2)
                    {
                        sum += measurements[j].Time;
                        ++total;
                        if(++j >= measurements.Count)
                            break;
                    }

                    var averageTime = sum / total;

                    for (int k = i; k < j; ++k )
                    {
                        measurements[k].Time = Convert.ToInt64(Math.Round(averageTime));
                    }

                    i = j;
                }
                else
                    ++i;
            }

            measurements.Sort(CompareByTime);
        }

        public bool ProcessFilesIntoOneFile(List<string> waypointFiles, List<string> measurementFiles, string outputFilename)
        {
            DasFilesWithoutWaypoints.Clear();
            
            ValidateMeasurementFiles(measurementFiles);

            //using(logFile = new StreamWriter("mcode_processing.log")) {

                // Delete file if it already exists.
                System.IO.File.Delete(outputFilename);

                foreach(string measurementFile in measurementFiles) {
                    List<DasTx> measurements = LoadDasTxMeasurement(measurementFile);

                    RemoveOutdatedMeasurements(measurements, measurementFile);

                    AdjustTimeStamps(measurements);

                    if(Options.ConvertHeightToMeters)
                        ConvertHeightToMeters(measurements);

                    var arrayOfWaypointsLists = createWaypointArrayList(measurementFile);

                    if(HasAssociatedWaypointFile(measurements, waypointFiles))
                        InterpolateFile(arrayOfWaypointsLists, measurements, measurementFile);
                    else {
                        //logFile.WriteLine(measurementFile + " using GPS.");
                        DasFilesWithoutWaypoints.Add(measurementFile);
                        ValidateMeasurementsUsingGps(measurements);
                    }

                    AppendToFileWithInputFilename(measurements, outputFilename);
                }
            //}
            return true;
        }

        public bool HasAssociatedWaypointFile(List<DasTx> measurements, List<string> waypointFiles)
        {
            bool hasAssociatedWaypointFile = false;

            if(measurements.Count > 0)
            {
                foreach(string file in waypointFiles)
                {
                    if (file.IndexOf(measurements[0].Filename.Substring(0, measurements[0].Filename.LastIndexOf("."))) != -1)
                    {
                        hasAssociatedWaypointFile = true;
                        break;
                    }
                }
            }
            return hasAssociatedWaypointFile;
        }

        public void ConvertHeightToMeters(List<DasTx> measurements)
        {
            foreach (DasTx meas in measurements)
                meas.Floor = (double.Parse(meas.Floor) * 0.3048).ToString();
        }

        private System.Collections.ArrayList LoadWaypoinsFiles(List<string> waypointFiles)
        {
            System.Collections.ArrayList ar = null;
            try
            {
                ar = new System.Collections.ArrayList(waypointFiles.Count);
                foreach (string waypointFile in waypointFiles)
                {
                    List<TimedCoordinate> waypoints = LoadWaypoins(waypointFile);
                    ar.Add(waypoints);
                }
            }
            catch (Exception exc)
            {
                OnException(exc, "One or more waypoint files cannot be loaded.");
            }
            return ar;
        }

        private void InterpolateFile(System.Collections.ArrayList listsOfWaypoints, List<DasTx> measurements, string filename)
        {
            bool processed = false;
            foreach(List<TimedCoordinate> waypointsList in listsOfWaypoints) {
                var pro = CalculateLonLat(waypointsList, measurements, filename);
                if(pro) {
                    //logFile.WriteLine(filename + "\tinterpolated\t" + measurements.Count + "\t measurements");
                    processed = pro;
                }
            }

            //if(!processed) {
            //    logFile.WriteLine(filename + "\tnot interpolated.");             
            //}
            if (Options.ConvertHeightToMeters)
                ConvertHeightToMeters(measurements);
        }

        public bool CreateOutput(string waypointFile, string measurementFile, string outputFile)
        {
            if (string.IsNullOrEmpty(waypointFile))
                throw new Exception("Provide valid waypoints file name.");
            if (string.IsNullOrEmpty(measurementFile))
                throw new Exception("Provide valid das measurements file name.");
            if (string.IsNullOrEmpty(waypointFile))
                throw new Exception("Provide valid output file name.");

            List<TimedCoordinate> waypoints = LoadWaypoins(waypointFile);
            List<DasTx> measurements = LoadDasTxMeasurement(measurementFile);

            CalculateLonLat(waypoints, measurements, outputFile);

            WriteToFile(measurements, outputFile);

            return true;
        }

        //calculate Lat & Lon for measurements list members
        public bool CalculateLonLat(List<TimedCoordinate> waypoints, List<DasTx> measurements, string filename)
        {
            int wp1_idx = 0;
            int wp2_idx = 0;
            int idx = 0;
            bool processed = false;

            try
            {
                if(waypoints.Count < 1) {
                    processed = false; //nothing to do. if empty input file, than create corresponding empty output file.
                }
                else if(measurements.Count < 1) {
                    processed = false;
                }
                //check if the same scanner
                else if(waypoints[0].ScannerID != measurements[0].ScannerID) {
                    processed = false;
                }
                else {
                    for(; idx < measurements.Count; idx++) {
                        while((wp2_idx < waypoints.Count) && (waypoints[wp2_idx].Time < measurements[idx].Time))
                            wp2_idx++;

                        if(wp2_idx >= waypoints.Count) {
                            //no more waypoints after this time
                            break;
                        }

                        if(waypoints[wp2_idx].Time == measurements[idx].Time) {
                            //an exact match
                            measurements[idx].valid = true;
                            measurements[idx].IsInterpolated = true;
                            measurements[idx].Lon = waypoints[wp2_idx].Lon;
                            measurements[idx].Lat = waypoints[wp2_idx].Lat;

                            measurements[idx].pixelX = waypoints[wp2_idx].pixelX;
                            measurements[idx].pixelY = waypoints[wp2_idx].pixelY;

                            measurements[idx].Floor = waypoints[wp2_idx].Floor;
                            measurements[idx].RasterFile = waypoints[wp2_idx].RasterFile;
                            processed = true;
                            continue;
                        }

                        wp1_idx = wp2_idx - 1;
                        if((wp1_idx < 0) || (waypoints[wp1_idx].Time > measurements[idx].Time)) {
                            //no waypoints yet by this time
                            continue;
                        }

                        if(waypoints[wp1_idx].Time == measurements[idx].Time) {
                            //an exact match
                            measurements[idx].valid = true;
                            measurements[idx].IsInterpolated = true;
                            measurements[idx].Lon = waypoints[wp1_idx].Lon;
                            measurements[idx].Lat = waypoints[wp1_idx].Lat;

                            measurements[idx].pixelX = waypoints[wp1_idx].pixelX;
                            measurements[idx].pixelY = waypoints[wp1_idx].pixelY;

                            measurements[idx].Floor = waypoints[wp1_idx].Floor;
                            measurements[idx].RasterFile = waypoints[wp1_idx].RasterFile;
                            processed = true;
                            continue;
                        }

                        GetCoordinate(waypoints[wp1_idx], waypoints[wp2_idx], measurements[idx]);
                        processed = true;
                    }
                }
            }
            catch (Exception exc)
            {
                OnException(exc, "Error while interpolating coordinates.");
            }
            return processed;
        }

        //coord contains Time on input and
        //Lat Lon is calculated.
        public bool GetCoordinate(TimedCoordinate waypoint1, TimedCoordinate waypoint2, DasTx coord)
        {
            //check if 2 waypoints have same floor/image
            if (waypoint1.Floor == waypoint2.Floor && waypoint1.RasterFile == waypoint2.RasterFile)
            {
                coord.Floor = waypoint1.Floor;
                coord.RasterFile = waypoint1.RasterFile;
            }
            else
            {
                return false;
            }

            double timeSinceWP1 = coord.Time - waypoint1.Time;
            double timeBetweenWPs = waypoint2.Time - waypoint1.Time;

            double delta = timeSinceWP1 / timeBetweenWPs; //(A2-D2)/(E2-D2)

            coord.Lon = waypoint1.Lon + (waypoint2.Lon - waypoint1.Lon) * delta; //K =(H2-G2)*F2+G2
            coord.Lat = waypoint1.Lat + (waypoint2.Lat - waypoint1.Lat) * delta; //L=(J2-I2)*F2+I2

            coord.pixelX = waypoint1.pixelX + (waypoint2.pixelX - waypoint1.pixelX) * delta;
            coord.pixelY = waypoint1.pixelY + (waypoint2.pixelY - waypoint1.pixelY) * delta;

            coord.valid = true;
            coord.IsInterpolated = true;

            return true;
        }

        public void WriteToFile(List<DasTx> measurements, string outputFile)
        {
            try
            {
                StreamWriter f = File.CreateText(outputFile);

                DasTx.WriteTmpHeader(f);

                foreach (DasTx meas in measurements)
                    meas.WriteToFileTmpVersion(f);

                f.Flush();
                f.Close();
            }
            catch (Exception exc)
            {
                OnException(exc, "Error while creating output file.");
            }
        }

        public void AppendToFileWithInputFilename(List<DasTx> measurements, string outputFile)
        {
            try
            {
                if (!File.Exists(outputFile))
                {
                    using (StreamWriter f = File.CreateText(outputFile))
                    {
                        DasTx.WriteTmpHeaderWithAdditionalFields(f);
                    }
                }

                using(StreamWriter f = File.AppendText(outputFile))
                {
                    foreach (DasTx meas in measurements)
                        meas.WriteToFileTmpVersionWithAdditionalFields(f);

                    f.Flush();
                    f.Close();
                }
            }
            catch (Exception exc)
            {
                OnException(exc, "Error while creating/appending output file.");
            }
        }

        private static int CompareByTime(SecondsSince1970 x, SecondsSince1970 y)
        {
            if (x.Time == y.Time)
                return 0;
            else if (x.Time > y.Time)
                return 1;
            else
                return -1;
        }

        private static int CompareByCollectionRound(DasTx x, DasTx y)
        {
            var xcr = long.Parse(x.CollectionRound);
            var ycr = long.Parse(y.CollectionRound);
            if (xcr == ycr)
                return 0;
            else if (xcr > ycr)
                return 1;
            else 
                return -1;
        }

        public List<TimedCoordinate> LoadWaypoins(string path)
        {
            List<TimedCoordinate> waypoints = null;

            try
            {
                waypoints = new List<TimedCoordinate>();

                using (StreamReader readFile = new StreamReader(path))
                {
                    //parsed line of data
                    string[] row;

                    //read a header
                    string csline = readFile.ReadLine();

                    if (csline != null)
                    {
                        while ((csline = readFile.ReadLine()) != null)
                        {
                            row = csline.Split('\t');
                            TimedCoordinate data = new TimedCoordinate(row);
                            waypoints.Add(data);
                        }
                    }
                }

                waypoints.Sort(CompareByTime);
            }
            catch (Exception exc)
            {
                OnException(exc, "Error while loading waypoints input file " + path + ".");
            }

            return waypoints;
        }

        public List<DasTx> LoadDasTxMeasurement(string path)
        {
            System.Collections.Generic.List<DasTx> measurements = null;

            try
            {

                measurements = new List<DasTx>();

                using (StreamReader readFile = new StreamReader(path))
                {
                    string filename = path.Substring(path.LastIndexOf('\\'), path.Length - path.LastIndexOf('\\'));
     
                    //parsed line of data
                    string[] row;

                    //read a header
                    string csline = readFile.ReadLine();

                    if (csline != null)
                    {
                        bool encrypted = csline.IndexOf("signal_levels") != -1;
                        while ((csline = readFile.ReadLine()) != null)
                        {
                            row = csline.Split('\t');
                            DasTx data = new DasTx(row, encrypted);
                            data.Filename = filename;
                            measurements.Add(data);
                        }
                    }
                }

                measurements.Sort(CompareByTime);
            }
            catch (Exception exc)
            {
                OnException(exc, "Error while loading das measurements input file " + path + ".");
            }

            return measurements;
        }

    }
}
