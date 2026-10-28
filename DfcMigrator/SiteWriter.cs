namespace DfcMigrator;

public static class SiteWriter
{
    // Writes into a staging folder first and swaps it in, so a failure never leaves a partial site.
    // An existing site is only replaced when asked. The old copy goes to <backupRoot>\<site>.bak-<timestamp>,
    // outside the website (a backup inside /Clients/ would be served by IIS), and is never deleted.
    public static (string Target, string? Backup) Write(GenerationResult r, string outputRoot, string site, string templateDir, bool replace, string backupRoot)
    {
        string root = Path.GetFullPath(outputRoot);
        string target = Path.Combine(root, site);
        string tmpl = Path.GetFullPath(templateDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if ((target + Path.DirectorySeparatorChar).StartsWith(tmpl, StringComparison.OrdinalIgnoreCase) ||
            tmpl.StartsWith(target + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Output folder overlaps the template folder - refusing so the template is never modified.");
        if (Directory.Exists(target) && !replace)
            throw new IOException($"Output folder already exists: {target}. Re-run with --replace to back it up and regenerate.");
        if (File.Exists(target)) throw new IOException($"A file with the site name exists: {target}");

        Directory.CreateDirectory(root);
        string staging = Path.Combine(root, $".{site}.staging-{Guid.NewGuid():N}");
        string? backup = null;
        try
        {
            foreach (var f in r.Files)
            {
                var path = Path.GetFullPath(Path.Combine(staging, f.RelPath));
                if (!path.StartsWith(staging + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new IOException($"Refusing to write outside the site folder: {f.RelPath}");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, f.Content);
            }
            if (Directory.Exists(target))
            {
                Directory.CreateDirectory(backupRoot);
                backup = Path.Combine(Path.GetFullPath(backupRoot), $"{site}.bak-{DateTime.Now:yyyyMMdd-HHmmss}");
                MoveDirectory(target, backup);
            }
            try { Directory.Move(staging, target); }
            catch when (backup is not null) { MoveDirectory(backup, target); backup = null; throw; } // put the old site back
            return (target, backup);
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true); // only our own staging copy
        }
    }

    // Directory.Move cannot cross drives/shares (e.g. Clients on a network share, backups on C:): copy, then remove the source.
    static void MoveDirectory(string from, string to)
    {
        if (string.Equals(Path.GetPathRoot(Path.GetFullPath(from)), Path.GetPathRoot(Path.GetFullPath(to)), StringComparison.OrdinalIgnoreCase))
        { Directory.Move(from, to); return; }
        foreach (var f in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var dest = Path.Combine(to, Path.GetRelativePath(from, f));
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(f, dest);
        }
        Directory.Delete(from, recursive: true);
    }
}
