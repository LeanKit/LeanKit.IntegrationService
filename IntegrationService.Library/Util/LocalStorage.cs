//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using System;
using System.IO;
using System.Reflection;
using ServiceStack.Text;

namespace IntegrationService.Util
{
    public interface ILocalStorage<T>
    {
        void SetPath(string filePath);
        void Save(T store);
        T Load();
    }


    public class LocalStorage<T>:ILocalStorage<T>
    {
        protected string StoragePath { get; set; }
        protected static readonly Logger _log = Logger.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly Object _localStorageFileLock = new object();

	    public LocalStorage()
	    {
		    var dir = new FileInfo(Assembly.GetExecutingAssembly().Location).Directory;
		    if (dir == null) throw new Exception("Could not access application directory.");
		    var curFolder = dir.FullName;
		    var storagefile = Path.Combine(curFolder, "localstore.json");
		    SetPath(storagefile);
	    }

	    public LocalStorage(string filePath)
        {
            SetPath(filePath);
        } 

        public void SetPath(string filePath)
        {
            if (!string.IsNullOrEmpty(filePath))
                StoragePath = filePath;
        }

        public void Save(T store)
        {
            if (StoragePath == null) return;

            string contents = JsonSerializer.SerializeToString(store);
            if (!string.IsNullOrEmpty(contents))
            {
                lock (_localStorageFileLock)
                {
                    File.WriteAllText(StoragePath, contents);
                }
            }
        }

        public T Load()
        {
            T localStorage = default(T);

            if (StoragePath == null) return localStorage;


            if (File.Exists(StoragePath))
            {
                string contents;
                lock (_localStorageFileLock)
                {
                    contents = File.ReadAllText(StoragePath);
                }
                if (!string.IsNullOrEmpty(contents))
                {
                    try
                    {
                        localStorage = JsonSerializer.DeserializeFromString<T>(contents);
                    }
                    catch (Exception ex)
                    {
                        _log.Warn(string.Format("Error reading local storage. {0} - {1} - {2}", ex.GetType(), ex.Message, ex.StackTrace));
                    }
                }
            }
            return localStorage;
        }
    }
}