using Fortress.NativeMessagingHost;

// ── Entry point ──────────────────────────────────────────────────────────────
// Chrome/Edge pass the extension origin as the first arg when launching us.
// We also support two management verbs:
//   --install [extension-id]   : writes the NativeMessagingHost registry key.
//                                If extension-id is omitted, falls back to the
//                                Chrome Web Store ID baked into RegistryInstaller.
//                                For an unpacked dev build pass the runtime ID
//                                shown in chrome://extensions (32-char lowercase).
//   --uninstall                : removes the registry keys and manifest.

if (args.Length > 0)
{
    switch (args[0])
    {
        case "--install":
            var extensionId = args.Length > 1 ? args[1].Trim() : null;
            RegistryInstaller.Install(extensionId);
            Console.WriteLine(extensionId is null
                ? "Fortress Native Messaging Host installed (default extension ID)."
                : $"Fortress Native Messaging Host installed for extension {extensionId}.");
            return 0;

        case "--uninstall":
            RegistryInstaller.Uninstall();
            Console.WriteLine("Fortress Native Messaging Host uninstalled.");
            return 0;
    }
}

// Normal run: bridge stdin ↔ pipe until the browser closes us.
var bridge = new PipeBridge();
await bridge.RunAsync(CancellationToken.None);
return 0;
