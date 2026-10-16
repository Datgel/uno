#nullable enable

namespace Windows.ApplicationModel.DataTransfer
{
	public static partial class Clipboard
	{
		/// <summary>
		/// Whether the clipboard currently holds text, decided WITHOUT reading the content wherever the platform
		/// allows it. Reading the content raises a privacy prompt or notice on some platforms (the iOS 16+ paste
		/// alert, the Android 12+ "pasted from your clipboard" toast), so a control that only needs to know whether
		/// Paste can be offered must ask this instead of <see cref="GetContent"/>.
		/// </summary>
		internal static bool ContainsText()
		{
			bool? result = null;
			ContainsTextPartial(ref result);
			return result ?? (GetContent()?.Contains(StandardDataFormats.Text) ?? false);
		}

		static partial void ContainsTextPartial(ref bool? result);
	}
}
