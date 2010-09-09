/******************************************************************************
 * FirePHP ASP.Net Server Library | CDub.FireNet.Logger
 * Copyright (C) 2008 Chris Winberry / Tautologistics
 * Email: chris(at)winberry(dot)net
 * Postal: 112 Pawnee Ave. | Oakland, NJ 07436
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace CDub.FireNet {

	/// <summary>
	/// Logging levels for FirePHP
	/// </summary>
	public enum LogLevel {
		/// <summary>
		/// General/default logging level
		/// </summary>
		LOG = 0,
		/// <summary>
		/// Informational messages
		/// </summary>
		INFO = 1,
		/// <summary>
		/// Problem messages but not necessarily errors
		/// </summary>
		WARN = 2,
		/// <summary>
		/// Error messages
		/// </summary>
		ERROR = 3,
		/// <summary>
		/// Exception and stack trace messages
		/// </summary>
		TRACE = 4,
		/// <summary>
		/// Data object dumping messages
		/// </summary>
		DUMP = 5
	}

	/// <summary>
	/// Represents a stack frame in the FirePHP TRACE format
	/// </summary>
	public struct StackFrameData {
		/// <summary>
		/// Source file
		/// </summary>
		public string file;
		/// <summary>
		/// Line number in the source code
		/// </summary>
		public int line;
		/// <summary>
		/// Current method in the stack frame
		/// </summary>
		public string function;
		/// <summary>
		/// Arguments passed to the method
		/// </summary>
		public string[] args;
	}

	/// <summary>
	/// Represents a stack trace in the FirePHP TRACE format
	/// </summary>
	public struct StackFrameDataContainer {
		/// <summary>
		/// Description/label for the stack trace
		/// </summary>
		public string Message;
		/// <summary>
		/// Source code file where trace originated
		/// </summary>
		public string File;
		/// <summary>
		/// Source code line number where trace originated
		/// </summary>
		public int Line;
		/// <summary>
		/// All the frames in the traced stack
		/// </summary>
		public StackFrameData[] Trace;
	}

	/// <summary>
	/// FirePHP/FireNet Logger for .Net
	/// </summary>
	public class Logger {
		// TODO: Detect Mono and use DateTime.Now instead of the this hack
		[DllImport("Kernel32.dll")]
		private static extern bool QueryPerformanceCounter (out long lpPerformanceCount);
		[DllImport("Kernel32.dll")]
		private static extern bool QueryPerformanceFrequency (out long lpFrequency);

		// Used to detect the presence of FirePHP in the UserAgent request header
		/// <summary>
		/// Regex used to detect FirePHP on the client
		/// </summary>
		static Regex _firePHPRegex;
		/// <summary>
		/// Pattern that will match a UserAgent header with FirePHP
		/// </summary>
		protected const string FIREPHP_PATTERN = @"\sFirePHP\/([\.|\d]*)\s?";

		// Defines for the FirePHP header format
		/// <summary>
		/// Prefix for all FirePHP headers
		/// </summary>
		protected const string FIREPHP_HEADER_PREFIX = "X-FirePHP-Data-";
		/// <summary>
		/// Token used to pad/balance arrays and hashes
		/// </summary>
		protected const string FIREPHP_SKIP_MACRO = "__SKIP__";
		/// <summary>
		/// JSON property that holds console messages
		/// </summary>
		protected const string FIREPHP_HEADER_CONSOLE = "FirePHP.Firebug.Console";
		/// <summary>
		/// Console message header section prefix
		/// </summary>
		protected const int FIREPHP_HEADER_CONSOLE_SECTION = 3;
		/// <summary>
		/// JSON property that holds dumped data objects
		/// </summary>
		protected const string FIREPHP_HEADER_DUMP = "FirePHP.Dump";
		/// <summary>
		/// Data dump header section prefix
		/// </summary>
		protected const int FIREPHP_HEADER_DUMP_SECTION = 2;

		/// <summary>
		/// Key used with HttpContext.Items to cache a request-specific Logger instance
		/// </summary>
		protected const string CONTEXT_KEY = "__FireNet__";

		/// <summary>
		/// Current HttpContext
		/// </summary>
		protected HttpContext _context;
		/// <summary>
		/// Flag indicating whether FirePHP was detected in the UserAgent request header
		/// </summary>
		protected Boolean _userAgentSupport = false;
		/// <summary>
		/// Flag indicating whether the basic FirePHP containing headers have been written
		/// </summary>
		protected Boolean _headersInit = false;
		/// <summary>
		/// Flag indicating whether the data dump containing headers have been written
		/// </summary>
		protected Boolean _headersDumpInit = false;
		/// <summary>
		/// Flag indicating whether the basic console message containing headers have been written
		/// </summary>
		protected Boolean _headersConsoleInit = false;

		/// <summary>
		/// Gets or creates an instance of Logger associated with the current HttpContext
		/// </summary>
		/// <returns>Logger instance for the current HttpContext</returns>
		public static Logger GetInstance () {
			HttpContext context = HttpContext.Current;

			if (null == context)
				return (new Logger());

			if (!context.Items.Contains(CONTEXT_KEY)
				|| context.Items[CONTEXT_KEY].GetType() != typeof(Logger)) {
				context.Items[CONTEXT_KEY] = new Logger();
			}

			return((Logger)context.Items[CONTEXT_KEY]);
		}

		static Logger () {
			_firePHPRegex = new Regex(FIREPHP_PATTERN,
				RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline
				);
		}

		/// <summary>
		/// Don't use this, use the GetInstance factory instead
		/// </summary>
		protected Logger () {
			_context = HttpContext.Current;
			if (null != _context)
				_userAgentSupport = _firePHPRegex.IsMatch(_context.Request.UserAgent);
		}

		/// <summary>
		/// Sets up the start and end headers for the FirePHP JSON object
		/// </summary>
		protected void InitHeaders () {
			if (_headersInit || !Enabled)
				return;

			WriteHeader("{", 1, 0, 1);
			WriteHeader(String.Format("\"{0}\":\"{0}\" }}", FIREPHP_SKIP_MACRO),
				9, 999, 99999999);

			_headersInit = true;
		}

		/// <summary>
		/// Sets up the start and end headers for the FirePHP.Dump section
		/// </summary>
		protected void InitDumpHeaders () {
			if (_headersDumpInit || !Enabled)
				return;

			InitHeaders();

			WriteHeader(String.Format("\"{0}\":{{", FIREPHP_HEADER_DUMP),
				2, 0, 1);
			WriteHeader(String.Format("\"{0}\":\"{0}\" }},", FIREPHP_SKIP_MACRO),
				2, 999, 99999999);

			_headersDumpInit = true;
		}

		/// <summary>
		/// Sets up the start and end headers for the FirePHP.Firebug.Console section
		/// </summary>
		protected void InitConsoleHeaders () {
			if (_headersConsoleInit || !Enabled)
				return;

			InitHeaders();

			WriteHeader(String.Format("\"{0}\":[", FIREPHP_HEADER_CONSOLE),
				FIREPHP_HEADER_CONSOLE_SECTION, 0, 1);
			WriteHeader(String.Format("[\"{0}\"]],", FIREPHP_SKIP_MACRO),
				FIREPHP_HEADER_CONSOLE_SECTION, 999, 99999999);

			_headersConsoleInit = true;
		}

		/// <summary>
		/// Formats a number with proper padding
		/// </summary>
		/// <param name="length">Number of 0s to pad</param>
		/// <param name="value">The number to pad</param>
		/// <returns>A 0-padded number as a string</returns>
		protected string FormatNumber (int length, long value) {
            string result = value.ToString().PadLeft(length, '0');
			return(result.Substring((result.Length - length), length));
		}

		/// <summary>
		/// The number of seconds since the UNIX timestamp epoch of 1970/01/01
		/// </summary>
		protected int CurrentSecs {
			get {
				return (Convert.ToInt32(
					(DateTime.Now - new DateTime(1970, 1, 1).ToLocalTime()).TotalSeconds
					));
			}
		}

		/// <summary>
		/// The number of microseconds elapsed
		/// </summary>
		protected long CurrentUSecs {
			get {
				long ticks = 0;
				QueryPerformanceCounter(out ticks);
				return (ticks / 10);
			}
		}

		/// <summary>
		/// Writes a FirePHP formatted header with provided data. Should probably use WriteHeader(string data, int section) instead
		/// </summary>
		/// <param name="data">The value of the header</param>
		/// <param name="section">Which sections to write the header to</param>
		/// <param name="secs">Elapsed seconds</param>
		/// <param name="msecs">Elapsed microseconds</param>
		protected void WriteHeader (string data, int section, int secs, long msecs) {
			string headerName = String.Format("{0}{1}{2}{3}",
				FIREPHP_HEADER_PREFIX,
				FormatNumber(1, section),
				FormatNumber(3, secs),
				FormatNumber(8, msecs)
				);
			_context.Response.AddHeader(headerName, data);
		}

		/// <summary>
		/// Writes a FirePHP formatted header with provided data
		/// </summary>
		/// <param name="data">The value of the header</param>
		/// <param name="section">Which sections to write the header to</param>
		protected void WriteHeader (string data, int section) {
			WriteHeader(
				data,
				section,
				CurrentSecs,
				CurrentUSecs
				);
		}

		/// <summary>
		/// Indicates whether the client is expecting FirePHP headers in the response
		/// </summary>
		public Boolean Enabled {
			get {
				return((null != _context) && _userAgentSupport);
			}
		}

		/// <summary>
		/// Logs a message with the LogLevel of LOG
		/// </summary>
		/// <param name="message">The message or data to log. If the parameter is an exception, the message will be TRACEd instead of LOGged</param>
		public void Log (Object message) {
			if (message is Exception)
				Trace((Exception)message);
			else
				Log(LogLevel.LOG, message);
		}

		/// <summary>
		/// Log a message
		/// </summary>
		/// <param name="level">Log level of the message</param>
		/// <param name="message">Message or data to log</param>
		public void Log (LogLevel level, Object message) {
			Log(level, null, message);
		}

		/// <summary>
		/// Log a message with a label
		/// </summary>
		/// <param name="level">Log level of the message</param>
		/// <param name="label">The label for the log message</param>
		/// <param name="message">Message or data to log</param>
		public void Log (LogLevel level, string label, Object message) {
			if (!Enabled)
				return;

			if (LogLevel.DUMP == level)
				InitDumpHeaders();
			else
				InitConsoleHeaders();

			string format;
			if (LogLevel.DUMP == level)
				format = "\"{1}\":{2},";
			else
				format = (null != label) ? "[\"{0}\",[\"{1}\",{2}]], " : "[\"{0}\",{2}],";

			WriteHeader(
				String.Format(format, level, label, Serializer.Serialize(message)),
				(LogLevel.DUMP == level) ? FIREPHP_HEADER_DUMP_SECTION : FIREPHP_HEADER_CONSOLE_SECTION
				);
		}

		/// <summary>
		/// Reads the frames out of a StackTrace into a StackFrameDataContainer
		/// </summary>
		/// <param name="trace">The frame stack trace</param>
		/// <returns>The frames in the trace that have a line number != 0</returns>
		protected StackFrameDataContainer ReadStackFrames (StackTrace trace) {
			StackFrame[] frames = trace.GetFrames();

			StackFrameDataContainer frameData = new StackFrameDataContainer();

            System.Collections.ArrayList framesTmp = new System.Collections.ArrayList();

            if (null == frames) {
                frameData.Trace = new StackFrameData[1] { new StackFrameData() };
                return (frameData);
            }

			for (int i = 0; i < frames.Length; i++) {
				if (frames[i].GetFileLineNumber() == 0)
					continue;

				if (framesTmp.Count == 0) {
				    frameData.File = frames[i].GetFileName();
				    frameData.Line = frames[i].GetFileLineNumber();
				}
                StackFrameData sfd = new StackFrameData();
                sfd.file = frames[i].GetFileName();
                sfd.line = frames[i].GetFileLineNumber();
                sfd.function = frames[i].GetMethod().Name;
				System.Reflection.ParameterInfo[] paramList = frames[i].GetMethod().GetParameters();
                sfd.args = new string[paramList.Length];
				for (int j = 0; j < paramList.Length; j++) {
                    sfd.args[j] = paramList[j].Name;
				}
                framesTmp.Add(sfd);
			}
            frameData.Trace = (StackFrameData[])framesTmp.ToArray(typeof(StackFrameData));
			
			return (frameData);
		}

		/// <summary>
		/// Dumps an object as a DUMP message
		/// </summary>
		/// <param name="label">Identifying label/key for the data being dumped</param>
		/// <param name="data">The data to dump</param>
		public void Dump (string label, Object data) {
			Log(LogLevel.DUMP, label, data);
		}

		/// <summary>
		/// Logs a stack trace along with the provided message
		/// </summary>
		/// <param name="message">The message to include with the trace</param>
		public void Trace (string message) {
			if (!Enabled)
				return;

			InitConsoleHeaders();

			StackFrameDataContainer frameData = ReadStackFrames(new System.Diagnostics.StackTrace(1, true));

			frameData.Message = (null == message) ? "Stack Trace" : message;

			this.Log(LogLevel.TRACE, null, frameData);
		}

		/// <summary>
		/// Logs an exception as a TRACE message with the stack trace unwound
		/// </summary>
		/// <param name="ex">The exception to trace and log</param>
		public void Trace (Exception ex) {
            if (null == ex)
                throw new ArgumentNullException("ex");

            if (!Enabled)
				return;

			InitConsoleHeaders();

			StackFrameDataContainer frameData = ReadStackFrames(new System.Diagnostics.StackTrace(ex, true));

			frameData.Message = ex.Message;

			this.Log(LogLevel.TRACE, null, frameData);
		}

	}

}
