namespace Sitecore.Shell.Applications.Media.MediaBrowser
{
  using Sitecore;
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
  using System;
  using System.Collections.Specialized;
  using System.Drawing;
  using System.IO;
  using System.Web.UI;

  public class MediaBrowserForm : DialogForm
  {
    protected XmlControl Dialog;
    protected Edit Filename;
    protected Scrollbox Listview;
    protected DataContext MediaDataContext;
    protected Button OpenWebDAVViewButton;
    protected TreeviewEx Treeview;
    protected Button UploadButton;

    private Item GetCurrentItem(Message message)
    {
      Assert.ArgumentNotNull(message, "message");
      string str = message["id"];
      Language language = Context.Language;
      Item folder = this.MediaDataContext.GetFolder();
      if (folder != null)
      {
        language = folder.Language;
      }
      if (!string.IsNullOrEmpty(str))
      {
        return Sitecore.Client.ContentDatabase.GetItem(str, language);
      }
      return folder;
    }

    public override void HandleMessage(Message message)
    {
      Assert.ArgumentNotNull(message, "message");
      if (message.Name == "item:load")
      {
        this.LoadItem(message);
      }
      else
      {
        Dispatcher.Dispatch(message, this.GetCurrentItem(message));
        base.HandleMessage(message);
      }
    }

    private static bool IsFolderItem(Item item)
    {
      Assert.ArgumentNotNull(item, "item");
      if (!(item.TemplateID == TemplateIDs.Node) && !(item.TemplateID == TemplateIDs.Folder))
      {
        return (item.TemplateID == TemplateIDs.MediaFolder);
      }
      return true;
    }

    protected void Listview_Click(string id)
    {
      Assert.ArgumentNotNullOrEmpty(id, "id");
      this.MediaDataContext.Folder = id;
      Item currentItem = this.GetCurrentItem(Message.Empty);
      if (currentItem != null)
      {
        this.UpdateSelection(currentItem);
      }
    }

    private void LoadItem(Message message)
    {
      Assert.ArgumentNotNull(message, "message");
      Item folder = this.MediaDataContext.GetFolder();
      if (folder != null)
      {
        Item item = Sitecore.Client.ContentDatabase.GetItem(ID.Parse(message["id"]), folder.Language);
        if (item != null)
        {
          this.UpdateSelection(item);
        }
      }
    }

    protected override void OnLoad(EventArgs e)
    {
      Assert.ArgumentNotNull(e, "e");
      base.OnLoad(e);
      if (!Context.ClientPage.IsEvent)
      {
        if (!WebDAVConfiguration.IsWebDAVEnabled(true))
        {
          this.OpenWebDAVViewButton.Visible = false;
        }
        MediaBrowserOptions options = MediaBrowserOptions.Parse();
        Item root = options.Root;
        Item selectedItem = options.SelectedItem;
        Language language = (root != null) ? root.Language : ((selectedItem != null) ? selectedItem.Language : null);
        Assert.IsNotNull(language, "Language can't be determined.");
        this.MediaDataContext.Language = language;
        if (root != null)
        {
          this.MediaDataContext.Root = root.ID.ToString();
        }
        if (selectedItem != null)
        {
          this.MediaDataContext.SetFolder(selectedItem.Uri);
        }
        Item folder = this.MediaDataContext.GetFolder();
        Assert.IsNotNull(folder, "Item not found.");
        this.UpdateSelection(folder);
      }
    }

    protected override void OnOK(object sender, EventArgs args)
    {
      Assert.ArgumentNotNull(sender, "sender");
      Assert.ArgumentNotNull(args, "args");
      MediaBrowserOptions options = MediaBrowserOptions.Parse();
      string str = this.Filename.Value;
      if (options.AllowEmpty && string.IsNullOrEmpty(str))
      {
        SheerResponse.SetDialogValue(string.Empty);
        base.OnOK(sender, args);
      }
      else if (string.IsNullOrEmpty(str))
      {
        SheerResponse.Alert(Translate.Text("Select a media item."), new string[0]);
      }
      else
      {
        Item root = this.MediaDataContext.GetRoot();
        if ((root != null) && (root.ID != root.Database.GetRootItem().ID))
        {
          str = FileUtil.MakePath(root.Paths.Path, str, '/');
        }
        Item item = this.MediaDataContext.GetItem(str);
        if (item == null)
        {
          SheerResponse.Alert(Translate.Text("The media item could not be found."), new string[0]);
        }
        else if (IsFolderItem(item))
        {
          this.MediaDataContext.SetFolder(item.Uri);
        }
        else
        {
          SheerResponse.SetDialogValue(item.ID.ToString());
          base.OnOK(sender, args);
        }
      }
    }

    protected void OpenWebDAVBrowser(ClientPipelineArgs args)
    {
      Assert.ArgumentNotNull(args, "args");
      string path = args.Parameters["id"];
      string name = args.Parameters["language"];
      string str3 = args.Parameters["version"];
      string str4 = args.Parameters["database"];
      Database database = Factory.GetDatabase(str4);
      Assert.IsNotNull(database, "database");
      Item item = database.GetItem(path, Language.Parse(name), Sitecore.Data.Version.Parse(str3));
      if (item == null)
      {
        SheerResponse.Alert(Translate.Text("Item not found."), new string[0]);
      }
      else if (!args.IsPostBack)
      {
        WebDAVOptions webDAVOptions = WebDAVUtil.GetWebDAVOptions(item);
        if (webDAVOptions == null)
        {
          SheerResponse.Alert(Translate.Text("Cannot create WebDAV Url."), new string[0]);
        }
        else
        {
          ID id = WebDAVConfiguration.SaveOptions(webDAVOptions);
          UrlString str5 = new UrlString(Context.Site.XmlControlPage);
          str5["xmlcontrol"] = "Sitecore.Shell.Applications.WebDAV.WebDAVBrowser";
          str5["oid"] = id.ToString();
          ModalDialogOptions options = new ModalDialogOptions(str5.ToString())
          {
            Width = "624",
            Height = "600",
            Response = true
          };
          SheerResponse.ShowModalDialog(options);
          args.WaitForPostBack();
        }
      }
      else
      {
        this.Treeview.Refresh(item);
      }
    }

    protected void OpenWebDAVView()
    {
      if (!WebDAVConfiguration.IsWebDAVEnabled(true))
      {
        Context.ClientPage.ClientResponse.Alert(Translate.Text("Drag & Drop is supported for IE only."));
      }
      else
      {
        Item selectionItem = this.Treeview.GetSelectionItem();
        if (selectionItem == null)
        {
          Context.ClientPage.ClientResponse.Alert(Translate.Text("Select an item first."));
        }
        else
        {
          selectionItem = WebDAVUtil.GetBrowseRootItem(selectionItem);
          NameValueCollection parameters = new NameValueCollection();
          parameters["id"] = selectionItem.ID.ToString();
          parameters["language"] = selectionItem.Language.ToString();
          parameters["version"] = selectionItem.Version.ToString();
          parameters["database"] = selectionItem.Database.Name;
          Context.ClientPage.Start(this, "OpenWebDAVBrowser", parameters);
        }
      }
    }

    private static void RenderEmpty(HtmlTextWriter output)
    {
      Assert.ArgumentNotNull(output, "output");
      output.Write("<table width=\"100%\" border=\"0\"><tr><td align=\"center\">");
      output.Write("<div style=\"padding:8px\">");
      output.Write(Translate.Text("This folder is empty."));
      output.Write("</div>");
      output.Write("<div class=\"scUploadLink\" style=\"padding:8px\">");
      new Tag("a") { Href = "#", Click = "scForm.postRequest('', '', '', 'UploadImage');", InnerHtml = Translate.Text("Upload a File.") + "." }.ToString(output);
      output.Write("</div>");
      output.Write("</td></tr></table>");
    }

    private static void RenderListviewItem(HtmlTextWriter output, Item item)
    {
      Assert.ArgumentNotNull(output, "output");
      Assert.ArgumentNotNull(item, "item");
      MediaItem item2 = item;
      output.Write("<a href=\"#\" class=\"scTile\" onclick=\"javascript:return scForm.postEvent(this,event,'Listview_Click(&quot;" + item.ID + "&quot;)')\">");
      output.Write("<div class=\"scTileImage\">");
      if (((item.TemplateID == TemplateIDs.Folder) || (item.TemplateID == TemplateIDs.TemplateFolder)) || (item.TemplateID == TemplateIDs.MediaFolder))
      {
        new ImageBuilder { Src = item.Appearance.Icon, Width = 0x30, Height = 0x30, Margin = "24px 24px 24px 24px" }.Render(output);
      }
      else
      {
        MediaUrlOptions thumbnailOptions = MediaUrlOptions.GetThumbnailOptions(item);
        thumbnailOptions.UseDefaultIcon = true;
        thumbnailOptions.Width = 0x60;
        thumbnailOptions.Height = 0x60;
        thumbnailOptions.Language = item.Language;
        thumbnailOptions.AllowStretch = false;
        output.Write("<img src=\"" + MediaManager.GetMediaUrl(item2, thumbnailOptions) + "\" class=\"scTileImageImage\" border=\"0\" alt=\"\" />");
      }
      output.Write("</div>");
      output.Write("<div class=\"scTileHeader\">");
      output.Write(item.GetUIDisplayName());
      output.Write("</div>");
      output.Write("</a>");
    }

    private static void RenderPreview(HtmlTextWriter output, Item item)
    {
      Assert.ArgumentNotNull(output, "output");
      Assert.ArgumentNotNull(item, "item");
      MediaItem item2 = item;
      MediaUrlOptions shellOptions = MediaUrlOptions.GetShellOptions();
      shellOptions.AllowStretch = false;
      shellOptions.BackgroundColor = Color.White;
      shellOptions.Language = item.Language;
      shellOptions.UseDefaultIcon = true;
      shellOptions.Height = 0xc0;
      shellOptions.Width = 0xc0;
      shellOptions.DisableBrowserCache = true;
      string mediaUrl = MediaManager.GetMediaUrl(item2, shellOptions);
      output.Write("<table width=\"100%\" height=\"100%\" border=\"0\"><tr><td align=\"center\">");
      output.Write("<div class=\"scPreview\">");
      output.Write("<img src=\"" + mediaUrl + "\" class=\"scPreviewImage\" border=\"0\" alt=\"\" />");
      output.Write("</div>");
      output.Write("<div class=\"scPreviewHeader\">");
      output.Write(item.GetUIDisplayName());
      output.Write("</div>");
      output.Write("</td></tr></table>");
    }

    protected void SelectTreeNode()
    {
      Item selectionItem = this.Treeview.GetSelectionItem(this.MediaDataContext.Language, Sitecore.Data.Version.Latest);
      if (selectionItem != null)
      {
        this.UpdateSelection(selectionItem);
      }
    }

    private string ShortenPath(string path)
    {
      Assert.ArgumentNotNull(path, "path");
      Item root = this.MediaDataContext.GetRoot();
      Assert.IsNotNull(root, "root");
      Item rootItem = root.Database.GetRootItem();
      Assert.IsNotNull(rootItem, "database root");
      if (root.ID != rootItem.ID)
      {
        string str = root.Paths.Path;
        if (path.StartsWith(str, StringComparison.InvariantCulture))
        {
          path = StringUtil.Mid(path, str.Length);
        }
      }
      return path;
    }

    protected void TreeViewDblClick()
    {
      if (this.Treeview.GetSelectionItem() != null)
      {
        this.OnOK(this, EventArgs.Empty);
      }
    }

    private void UpdateSelection(Item item)
    {
      Assert.ArgumentNotNull(item, "item");
      this.OpenWebDAVViewButton.Visible = WebDAVUtil.IsWebDAVAvailableFor(item);
      this.Filename.Value = this.ShortenPath(item.Paths.Path);
      this.MediaDataContext.SetFolder(item.Uri);
      this.Treeview.SetSelectedItem(item);
      HtmlTextWriter output = new HtmlTextWriter(new StringWriter());
      if (((item.TemplateID == TemplateIDs.Folder) || (item.TemplateID == TemplateIDs.MediaFolder)) || (item.TemplateID == TemplateIDs.MainSection))
      {
        foreach (Item item2 in item.Children)
        {
          if (item2.Appearance.Hidden)
          {
            if (Context.User.IsAdministrator && UserOptions.View.ShowHiddenItems)
            {
              RenderListviewItem(output, item2);
            }
          }
          else
          {
            RenderListviewItem(output, item2);
          }
        }
      }
      else
      {
        RenderPreview(output, item);
      }
      string str = output.InnerWriter.ToString();
      if (string.IsNullOrEmpty(str))
      {
        RenderEmpty(output);
        str = output.InnerWriter.ToString();
      }
      this.Listview.InnerHtml = str;
      bool flag = item.Access.CanCreate();
      this.UploadButton.Disabled = !flag;
      this.OpenWebDAVViewButton.Disabled = !flag;
    }

    protected void UploadImage()
    {
      Item currentItem = this.GetCurrentItem(Message.Empty);
      if (currentItem == null)
      {
        SheerResponse.Alert(Translate.Text("Item not found."), new string[0]);
      }
      else if (!currentItem.Access.CanCreate())
      {
        SheerResponse.Alert(Translate.Text("You do not have permission to create a new item here."), new string[0]);
      }
      else
      {
        Context.ClientPage.SendMessage(this, "media:upload(edit=1,load=1,tofolder=1)");
      }
    }
  }
}
