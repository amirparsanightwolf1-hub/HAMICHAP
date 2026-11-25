# Artwork upload regression checks

The shared roll/sheet job editor now reads each selection immediately into a private temporary directory outside the web root. The saved job is updated only on the existing save action. Each staged file is limited to 25 MiB; at most 20 pending files may be staged per save. Keep the IIS app-pool identity able to write its temporary directory and existing artwork folder. Temporary files are removed when the selection is discarded, the job is successfully saved, or the editor is disposed; abrupt process termination may leave private temp files (remove abandoned HamiChap-artwork subdirectories when the app is stopped). Previously saved files are not deleted.

After deploying, test both roll and sheet jobs:
1. Select artwork A, wait until uploading finishes, then select artwork B separately. Save and open approval proof. Both should appear without the Blazor file-ID error.
2. Select several files at once, then select another batch. Verify all are attached and have intact content.
3. Replace a previously saved file, select another replacement, and remove a pending file. Confirm the intended final set and verify prior approval-proof files remain available.
4. Try an invalid extension, oversized/empty file, and a 21st pending file. Check the error and verify previously staged files remain available.
5. While uploading a large file, try Save and select another file. Save must not proceed until staging completes. Select the same file again after staging; the picker should fire a fresh change event.
6. Trigger a failed database save and retry. Staged bytes should remain available; newly copied final files from the failed attempt must be removed.
7. Navigate away before saving or cancel a pending file. Verify unsaved files are cleaned up; existing job references are unchanged.
8. Deploy requires no database migration for this upload fix.
