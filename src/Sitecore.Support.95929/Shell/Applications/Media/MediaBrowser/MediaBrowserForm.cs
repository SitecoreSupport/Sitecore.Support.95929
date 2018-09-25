// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MediaBrowser.form.cs" company="Sitecore">
//   Copyright (c) Sitecore. All rights reserved.
// </copyright>
// <summary>
//   Media Browser form.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Sitecore.Support.Shell.Applications.Media.MediaBrowser
{
  using System;
  using System.Collections.Specialized;
  using System.Drawing;
  using System.IO;
  using System.Web.UI;
  using Sitecore.Configuration;
  using Sitecore.Data;
  using Sitecore.Data.Items;
  using Sitecore.Diagnostics;
  using Sitecore.Globalization;
  using Sitecore.IO;
  using Sitecore.Resources;
  using Sitecore.Resources.Media;
  using Sitecore.Shell;
  using Sitecore.Shell.Applications.Dialogs.MediaBrowser;
  using Sitecore.Shell.Framework;
  using Sitecore.Text;
  using Sitecore.Web.UI.HtmlControls;
  using Sitecore.Web.UI.Pages;
  using Sitecore.Web.UI.Sheer;
  using Sitecore.Web.UI.WebControls;
  using Sitecore.Web.UI.XmlControls;

  /// <summary>
  /// Media Browser form.
  /// </summary>
  public class MediaBrowserForm : DialogForm
  {
    #region Controls

    /// <summary></summary>
    protected XmlControl Dialog;

    /// <summary>
    /// The media data context.
    /// </summary>
    protected DataContext MediaDataContext;

    /// <summary>
    /// The tree view of content items.
    /// </summary>
    protected TreeviewEx Treeview;

    /// <summary>
    /// The edit field for file name.
    /// </summary>
    protected Edit Filename;

    /// <summary>
    /// The scroll box for list view.
    /// </summary>
    protected Scrollbox Listview;

    /// <summary>
    /// Button for openning Web DAV view.
    /// </summary>
    protected Button OpenWebDAVViewButton;

    /// <summary>
    /// The upload button
    /// </summary>
    protected Button UploadButton;

    #endregion

    #region Public methods

    /// <summary>
    /// Handles the message.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <contract>
    ///   <requires name="message" condition="not null" />
    /// </contract>
    public override void HandleMessage([NotNull] Message message)
    {
      Assert.ArgumentNotNull(message, "message");

      if (message.Name == "item:load")
      {
        this.LoadItem(message);
        return;
      }

      Dispatcher.Dispatch(message, this.GetCurrentItem(message));

      base.HandleMessage(message);
    }

    #endregion

    #region Protected methods

    /// <summary>
    /// Raises the load event.
    /// </summary>
    /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
    /// <contract>
    ///   <requires name="e" condition="not null" />
    /// </contract>
    protected override void OnLoad([NotNull] EventArgs e)
    {
      Assert.ArgumentNotNull(e, "e");

      base.OnLoad(e);

      if (Context.ClientPage.IsEvent)
      {
        return;
      }

      if (!WebDAVConfiguration.IsWebDAVEnabled(true))
      {
        this.OpenWebDAVViewButton.Visible = false;
      }

      MediaBrowserOptions options = MediaBrowserOptions.Parse();

      Item root = options.Root;
      Item selectedItem = options.SelectedItem;
      Language lang = (root != null) ? root.Language : (selectedItem != null) ? selectedItem.Language : null;
      Assert.IsNotNull(lang, "Language can't be determined.");

      // Make sure language is set for MediaContext
      this.MediaDataContext.Language = lang;

      if (root != null)
      {
        this.MediaDataContext.Root = root.ID.ToString();
      }
      
      if (selectedItem != null)
      {
        this.MediaDataContext.SetFolder(selectedItem.Uri); //Folder = selectedItem.ID.ToString()
      }

      Item folder = this.MediaDataContext.GetFolder();
      Assert.IsNotNull(folder, Texts.ITEM_NOT_FOUND);

      this.UpdateSelection(folder);
    }

    /// <summary>
    /// Handles the Listview_ click event.
    /// </summary>
    /// <param name="id">The id.</param>
    protected void Listview_Click([NotNull] string id)
    {
      Assert.ArgumentNotNullOrEmpty(id, "id");

      this.MediaDataContext.Folder = id;

      Item item = this.GetCurrentItem(Message.Empty);
      if (item == null)
      {
        return;
      }

      this.UpdateSelection(item);
    }

    /// <summary>
    /// Handles a click on the OK button.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="args">The arguments.</param>
    /// <contract>
    ///   <requires name="sender" condition="not null"/>
    ///   <requires name="args" condition="not null"/>
    ///   </contract>
    /// <remarks>
    /// When the user clicks OK, the dialog is closed by calling
    /// the <see cref="Sitecore.Web.UI.Sheer.ClientResponse.CloseWindow">CloseWindow</see> method.
    /// </remarks>
    protected override void OnOK([NotNull] object sender, [NotNull] EventArgs args)
    {
      Assert.ArgumentNotNull(sender, "sender");
      Assert.ArgumentNotNull(args, "args");

      MediaBrowserOptions options = MediaBrowserOptions.Parse();

      string path = this.Filename.Value;

      if (options.AllowEmpty && string.IsNullOrEmpty(path))
      {
        SheerResponse.SetDialogValue(string.Empty);
        base.OnOK(sender, args);
        return;
      }

      if (string.IsNullOrEmpty(path))
      {
        SheerResponse.Alert(Translate.Text(Texts.PLEASE_SELECT_A_MEDIA_ITEM));
        return;
      }

      Item root = this.MediaDataContext.GetRoot();

      if (root != null && root.ID != root.Database.GetRootItem().ID)
      {
        #region FIX 95929
        string text2 = path;
        path = FileUtil.MakePath(root.Paths.Path, path, '/');
        string text3 = root.Paths.Path.Replace("/sitecore/media library", "");
        if (text2.Contains(text3))
        {
          path = FileUtil.MakePath(root.Paths.Path, text2.Replace(text3, ""), '/');
        }
        #endregion
      }

      Item item = this.MediaDataContext.GetItem(path);

      if (item == null)
      {
        SheerResponse.Alert(Translate.Text(Texts.THE_MEDIA_ITEM_COULD_NOT_BE_FOUND));
        return;
      }

      if (IsFolderItem(item))
      {
        this.MediaDataContext.SetFolder(item.Uri);
        return;
      }

      SheerResponse.SetDialogValue(item.ID.ToString());

      base.OnOK(sender, args);
    }

    /// <summary>
    /// Selects the tree node.
    /// </summary>
    protected void SelectTreeNode()
    {
      Item item = this.Treeview.GetSelectionItem(this.MediaDataContext.Language, Sitecore.Data.Version.Latest);

      if (item == null)
      {
        return;
      }

      this.UpdateSelection(item);
    }

    /// <summary>
    /// Handles double click in the treeview
    /// </summary>
    protected void TreeViewDblClick()
    {
      Item item = this.Treeview.GetSelectionItem();

      if (item == null)
      {
        return;
      }

      this.OnOK(this, EventArgs.Empty);
    }

    /// <summary>
    /// Uploads the image.
    /// </summary>
    protected void UploadImage()
    {
      Item item = this.GetCurrentItem(Message.Empty);
      if (item == null)
      {
        SheerResponse.Alert(Translate.Text(Texts.ITEM_NOT_FOUND));
        return;
      }

      if (!item.Access.CanCreate())
      {
        SheerResponse.Alert(Translate.Text(Texts.YOU_DO_NOT_HAVE_PERMISSION_TO_CREATE_A_NEW_ITEM_HERE));
        return;
      }

      Context.ClientPage.SendMessage(this, "media:upload(edit=1,load=1,tofolder=1)");
    }

    /// <summary>
    /// Opens the web DAV view.
    /// </summary>
    protected void OpenWebDAVView()
    {
      if (!WebDAVConfiguration.IsWebDAVEnabled(true))
      {
        Context.ClientPage.ClientResponse.Alert(Translate.Text(Texts.DragDropissupportedforIEonly));
        return;
      }

      Item item = this.Treeview.GetSelectionItem();

      if (item == null)
      {
        Context.ClientPage.ClientResponse.Alert(Translate.Text(Texts.PLEASE_SELECT_AN_ITEM_FIRST));
        return;
      }

      item = WebDAVUtil.GetBrowseRootItem(item);
      var parameters = new NameValueCollection();
      parameters["id"] = item.ID.ToString();
      parameters["language"] = item.Language.ToString();
      parameters["version"] = item.Version.ToString();
      parameters["database"] = item.Database.Name;

      Context.ClientPage.Start(this, "OpenWebDAVBrowser", parameters);
    }

    /// <summary>
    /// Opens the web DAV browser.
    /// </summary>
    /// <param name="args">The arguments.</param>
    protected void OpenWebDAVBrowser(ClientPipelineArgs args)
    {
      Assert.ArgumentNotNull(args, "args");

      string id = args.Parameters["id"];
      string language = args.Parameters["language"];
      string version = args.Parameters["version"];
      string databaseName = args.Parameters["database"];

      Database database = Factory.GetDatabase(databaseName);
      Assert.IsNotNull(database, "database");
      Item item = database.GetItem(id, Language.Parse(language), Sitecore.Data.Version.Parse(version));

      if (item == null)
      {
        SheerResponse.Alert(Translate.Text(Texts.ITEM_NOT_FOUND));
        return;
      }

      if (!args.IsPostBack)
      {
        WebDAVOptions options = WebDAVUtil.GetWebDAVOptions(item);
        if (options == null)
        {
          SheerResponse.Alert(Translate.Text(Texts.CannotcreateWebDavUrl));
          return;
        }

        ID optionsID = WebDAVConfiguration.SaveOptions(options);
        var url = new UrlString(Context.Site.XmlControlPage);
        url["xmlcontrol"] = "Sitecore.Shell.Applications.WebDAV.WebDAVBrowser";
        url["oid"] = optionsID.ToString();
        SheerResponse.ShowModalDialog(new ModalDialogOptions(url.ToString()) { Width = "624", Height = "600", Response = true });
        args.WaitForPostBack();
      }
      else
      {
        this.Treeview.Refresh(item);
      }
    }

    #endregion

    #region Private methods

    /// <summary>
    /// Determines whether the specified item is a folder item.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>
    ///   <c>true</c> if the specified item is a folder item; otherwise, <c>false</c>.
    /// </returns>
    /// <contract>
    ///   <requires name="item" condition="not null" />
    /// </contract>
    private static bool IsFolderItem([NotNull] Item item)
    {
      Assert.ArgumentNotNull(item, "item");

      return item.TemplateID == TemplateIDs.Node ||
        item.TemplateID == TemplateIDs.Folder ||
          item.TemplateID == TemplateIDs.MediaFolder;
    }

    /// <summary>
    /// Renders the empty.
    /// </summary>
    /// <param name="output">The output.</param>
    private static void RenderEmpty([NotNull] HtmlTextWriter output)
    {
      Assert.ArgumentNotNull(output, "output");

      output.Write("<table width=\"100%\" border=\"0\"><tr><td align=\"center\">");

      output.Write("<div style=\"padding:8px\">");
      output.Write(Translate.Text(Texts.THIS_FOLDER_IS_EMPTY));
      output.Write("</div>");

      output.Write("<div class=\"scUploadLink\" style=\"padding:8px\">");
      new Tag("a")
      {
        Href = "#",
        Click = "scForm.postRequest('', '', '', 'UploadImage');",
        InnerHtml = Translate.Text(Texts.UPLOAD_A_FILE) + "."
      }.ToString(output);
      output.Write("</div>");

      output.Write("</td></tr></table>");
    }

    /// <summary>
    /// Renders the list view item.
    /// </summary>
    /// <param name="output">The output.</param>
    /// <param name="item">The child.</param>
    private static void RenderListviewItem([NotNull] HtmlTextWriter output, [NotNull] Item item)
    {
      Assert.ArgumentNotNull(output, "output");
      Assert.ArgumentNotNull(item, "item");

      MediaItem mediaItem = item;

      output.Write("<a href=\"#\" class=\"scTile\" onclick=\"javascript:return scForm.postEvent(this,event,'Listview_Click(&quot;" + item.ID + "&quot;)')\">");

      output.Write("<div class=\"scTileImage\">");

      if (item.TemplateID == TemplateIDs.Folder || item.TemplateID == TemplateIDs.TemplateFolder || item.TemplateID == TemplateIDs.MediaFolder)
      {
        new ImageBuilder
        {
          Src = item.Appearance.Icon,
          Width = 48,
          Height = 48,
          Margin = "24px 24px 24px 24px"
        }.Render(output);
      }
      else
      {
        MediaUrlOptions options = MediaUrlOptions.GetThumbnailOptions(item);

        options.UseDefaultIcon = true;
        options.Width = 96;
        options.Height = 96;
        options.Language = item.Language;
        options.AllowStretch = false;

        string src = MediaManager.GetMediaUrl(mediaItem, options);
        output.Write("<img src=\"" + src + "\" class=\"scTileImageImage\" border=\"0\" alt=\"\" />");
      }

      output.Write("</div>");

      output.Write("<div class=\"scTileHeader\">");
      output.Write(item.GetUIDisplayName());
      output.Write("</div>");

      output.Write("</a>");
    }

    /// <summary>
    /// Renders the preview.
    /// </summary>
    /// <param name="output">The output.</param>
    /// <param name="item">The item.</param>
    private static void RenderPreview([NotNull] HtmlTextWriter output, [NotNull] Item item)
    {
      Assert.ArgumentNotNull(output, "output");
      Assert.ArgumentNotNull(item, "item");

      MediaItem mediaItem = item;

      var options = MediaUrlOptions.GetShellOptions();

      options.AllowStretch = false;
      options.BackgroundColor = Color.White;
      options.Language = item.Language;
      options.UseDefaultIcon = true;
      options.Height = 192;
      options.Width = 192;
      options.DisableBrowserCache = true;

      string src = MediaManager.GetMediaUrl(mediaItem, options);

      output.Write("<table width=\"100%\" height=\"100%\" border=\"0\"><tr><td align=\"center\">");

      output.Write("<div class=\"scPreview\">");
      output.Write("<img src=\"" + src + "\" class=\"scPreviewImage\" border=\"0\" alt=\"\" />");
      output.Write("</div>");
      output.Write("<div class=\"scPreviewHeader\">");
      output.Write(item.GetUIDisplayName());
      output.Write("</div>");

      output.Write("</td></tr></table>");
    }

    /// <summary>
    /// Loads the item.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <contract>
    ///   <requires name="message" condition="not null" />
    /// </contract>
    private void LoadItem([NotNull] Message message)
    {
      Assert.ArgumentNotNull(message, "message");

      Item folder = this.MediaDataContext.GetFolder();
      if (folder == null)
      {
        return;
      }

      Item item = Client.ContentDatabase.GetItem(ID.Parse(message["id"]), folder.Language);
      if (item == null)
      {
        return;
      }

      this.UpdateSelection(item);
    }

    /// <summary>
    /// Gets the current item.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>
    /// The current item.
    /// </returns>
    [CanBeNull]
    private Item GetCurrentItem([NotNull] Message message)
    {
      Assert.ArgumentNotNull(message, "message");

      string id = message["id"];

      Language language = Context.Language;

      Item folder = this.MediaDataContext.GetFolder();
      if (folder != null)
      {
        language = folder.Language;
      }

      if (!string.IsNullOrEmpty(id))
      {
        return Client.ContentDatabase.GetItem(id, language);
      }

      return folder;
    }

    /// <summary>
    /// Shortens the path.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The shorten path.</returns>
    /// <contract>
    ///   <requires name="path" condition="not null"/>
    ///   <ensures condition="nullable"/>
    /// </contract>
    [CanBeNull]
    private string ShortenPath([NotNull] string path)
    {
      Assert.ArgumentNotNull(path, "path");

      Item root = this.MediaDataContext.GetRoot();
      Assert.IsNotNull(root, "root");

      Item rootItem = root.Database.GetRootItem();
      Assert.IsNotNull(rootItem, "database root");

      if (root.ID != rootItem.ID)
      {
        string rootPath = root.Paths.Path;

        if (path.StartsWith(rootPath, StringComparison.InvariantCulture))
        {
          path = StringUtil.Mid(path, rootPath.Length);
        }
      }

      return path;
    }

    /// <summary>
    /// Updates the list view.
    /// </summary>
    /// <param name="item">The item.</param>
    private void UpdateSelection([NotNull] Item item)
    {
      Assert.ArgumentNotNull(item, "item");

      OpenWebDAVViewButton.Visible = WebDAVUtil.IsWebDAVAvailableFor(item);

      this.Filename.Value = this.ShortenPath(item.Paths.Path);
      this.MediaDataContext.SetFolder(item.Uri);
      this.Treeview.SetSelectedItem(item);

      var output = new HtmlTextWriter(new StringWriter());

      if (item.TemplateID == TemplateIDs.Folder || item.TemplateID == TemplateIDs.MediaFolder || item.TemplateID == TemplateIDs.MainSection)
      {
        foreach (Item child in item.Children)
        {
          if (child.Appearance.Hidden)
          {
            if (Context.User.IsAdministrator && UserOptions.View.ShowHiddenItems)
            {
              RenderListviewItem(output, child);
            }

            continue;
          }

          RenderListviewItem(output, child);
        }
      }
      else
      {
        RenderPreview(output, item);
      }

      string text = output.InnerWriter.ToString();

      if (string.IsNullOrEmpty(text))
      {
        RenderEmpty(output);
        text = output.InnerWriter.ToString();
      }

      this.Listview.InnerHtml = text;

      var canUpload = item.Access.CanCreate();

      this.UploadButton.Disabled = !canUpload;
      this.OpenWebDAVViewButton.Disabled = !canUpload;
    }

    #endregion
  }
}
