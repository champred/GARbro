//! \file       Program.cs
//! \date       Mon Jun 30 20:12:13 2014
//! \brief      game resources browser.
//

using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using GameRes;

namespace GARbro
{
    class ConsoleBrowser
    {
        private string      m_arc_name;
        private ImageFormat m_image_format;
        private bool        m_extract_all;

        void ListFormats ()
        {
            Console.WriteLine ("Recognized resource formats:");
            foreach (var impl in FormatCatalog.Instance.ArcFormats)
            {
                Console.WriteLine ("{0,-4} {1}", impl.Tag, impl.Description);
            }
        }

        void ExtractAll (ArcFile arc)
        {
            arc.ExtractFiles ((i, entry, msg) => {
                if (null != entry)
                {
                    Console.WriteLine ("Extracting {0} ...", entry.Name);
                }
                else if (null != msg)
                {
                    Console.WriteLine (msg);
                }
                return ArchiveOperation.Continue;
            });
        }

        void ExtractFile (ArcFile arc, string name)
        {
            Entry entry = arc.Dir.FirstOrDefault (e => e.Name.Equals (name, StringComparison.OrdinalIgnoreCase));
            if (null == entry)
            {
                Console.Error.WriteLine ("'{0}' not found within {1}", name, m_arc_name);
                return;
            }
            Console.WriteLine ("Extracting {0} ...", entry.Name);
            arc.Extract (entry);
        }


        ImageFormat FindFormat (string format)
        {
            var range = FormatCatalog.Instance.LookupExtension<ImageFormat> (format);
            return range.FirstOrDefault();
        }

        void Run (string[] args)
        {
            int argn = 0;
            while (argn < args.Length)
            {
                if (args[argn].Equals ("-l"))
                {
                    ListFormats();
                    return;
                }
                else if (args[argn].Equals ("-c"))
                {
                    var tag = args[argn+1];
                    m_image_format = ImageFormat.FindByTag (tag);
                    if (null == m_image_format)
                    {
                        Console.Error.WriteLine ("{0}: unknown format specified", tag);
                        return;
                    }
                    argn += 2;
                }
                else if (args[argn].Equals ("-x"))
                {
                    m_extract_all = true;
                    ++argn;
                }
                else if (args[argn].Equals ("-g"))
                {
                    m_arc_name = args[++argn];
                    ++argn;
                }
                else
                {
                    break;
                }
                if (argn >= args.Length)
                {
                        Usage();
                        return;
                }
            }
            DeserializeGameData();
            foreach (var file in VFS.GetFiles (args[argn]))
            {
                file.Game = m_arc_name;
                ArcFile arc;
                try {
                    arc = ArcFile.TryOpen(file);
                } catch (NotImplementedException) {
                    Console.WriteLine("Could not find '{0}' in the games list.", m_arc_name);
                    return;
                } catch (OperationCanceledException) {
                    Console.WriteLine("Consider using the -g option to specify which game the archive is for.");
                    return;
                } catch { return; }
                if (args.Length > argn+1)
                {
                    for (int i = argn+1; i < args.Length; ++i)
                        ExtractFile (arc, args[i]);
                }
                else if (m_extract_all)
                {
                    ExtractAll (arc);
                }
                else
                {
                    foreach (var entry in arc.Dir.OrderBy (e => e.Offset))
                    {
                        Console.WriteLine ("{0,9} [{2:X8}] {1}", entry.Size, entry.Name, entry.Offset);
                    }
                }
            }
        }

        void DeserializeGameData ()
        {
            string scheme_file = Path.Combine (FormatCatalog.Instance.DataDirectory, "Formats.dat");
            try
            {
                using (var file = File.OpenRead (scheme_file))
                    FormatCatalog.Instance.DeserializeScheme (file);
            }
            catch (Exception X)
            {
                Console.Error.WriteLine ("Scheme deserialization failed: {0}", X.Message);
            }
        }

        static string ProgramName
        {
                get
                {
                        return Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetExecutingAssembly().Location);
                }
        }

        static void Usage ()
        {
            Console.WriteLine ("Usage: {0} [OPTIONS] ARC [ENTRIES]", ProgramName);
            Console.WriteLine ("    -l          list recognized archive formats");
            Console.WriteLine ("    -x          extract all files");
            Console.WriteLine ("    -c FORMAT   convert images to specified format");
            Console.WriteLine ("    -g GAME     use game name for decryption");
            Console.WriteLine ("Games list: https://morkt.github.io/GARbro/supported.html");
            Console.WriteLine ("Without options displays contents of specified archive.");
        }

        static void Main (string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            if (0 == args.Length)
            {
                Usage();
                return;
            }
            var listener = new TextWriterTraceListener (Console.Error);
            Trace.Listeners.Add(listener);
            try
            {
                var browser = new ConsoleBrowser();
                browser.Run (args);
            }
            catch (Exception X)
            {
                Console.Error.WriteLine (X.Message);
            }
        }
    }
}
