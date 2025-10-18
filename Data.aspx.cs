using System;
using System.Web.UI;

namespace ClintonFrankland
{
    public partial class Data : Page
    {
        public Data()
        {
            Load += Page_Load;
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!(Request["command"] == null))
            {
                using (var sql = new SqlProvider(Properties.Settings.Default.SiteSqlString))
                {
                    switch (Request["command"] ?? "")
                    {
                        case "GetRegionId":
                            {
                                try
                                {
                                    int intRegionId = sql.GetInteger("clintonfrankland_db", "dbo", "spwtGetRegionId", new NamedValue("regionname", GetRequestParameter("regionname")), new NamedValue("regionhost", GetRequestParameter("regionhost")), new NamedValue("detected", Convert.ToDateTime(GetRequestParameter("detected"))));


                                    litMain.Text = "<REGIONID>" + intRegionId.ToString() + "<REGIONID>";
                                }
                                catch (Exception ex)
                                {
                                    litMain.Text = "<ERROR>" + ex.GetType().ToString() + "<br />" + ex.Message.ToString() + "<ERROR>";
                                }

                                break;
                            }
                        case "GetOwnerAgentId":
                            {
                                try
                                {
                                    int intOwnerAgentId = sql.GetInteger("clintonfrankland_db", "dbo", "spWtGetOwnerAgentId", new NamedValue("agentkey", GetRequestParameter("agentkey")), new NamedValue("agentname", GetRequestParameter("agentname")), new NamedValue("displayname", GetRequestParameter("displayname")), new NamedValue("agentlanguage", GetRequestParameter("agentlanguage")), new NamedValue("detected", Convert.ToDateTime(GetRequestParameter("detected"))));




                                    litMain.Text = "<OWNERAGENTID>" + intOwnerAgentId.ToString() + "<OWNERAGENTID>";
                                }
                                catch (Exception ex)
                                {
                                    litMain.Text = "<ERROR>" + ex.GetType().ToString() + "<br />" + ex.Message.ToString() + "<ERROR>";
                                }

                                break;
                            }
                        case "GetParcelId":
                            {
                                try
                                {
                                    int intParcelId = sql.GetInteger("clintonfrankland_db", "dbo", "spWtGetParcelId", new NamedValue("parcelKey", GetRequestParameter("parcelkey")), new NamedValue("parcelName", GetRequestParameter("parcelname")), new NamedValue("parcelDesc", GetRequestParameter("parceldesc")), new NamedValue("parcelArea", int.Parse(GetRequestParameter("parcelarea"))), new NamedValue("parcelOwnerKey", GetRequestParameter("parcelownerkey")), new NamedValue("parcelOwnerName", GetRequestParameter("parcelownername")), new NamedValue("parcelOwnerLanguage", GetRequestParameter("parcelownerlanguage")), new NamedValue("parcelOwnerDisplayname", GetRequestParameter("parcelownerdisplayname")), new NamedValue("parcelGroupKey", GetRequestParameter("parcelgroupkey")), new NamedValue("regionId", int.Parse(GetRequestParameter("regionid"))), new NamedValue("detected", Convert.ToDateTime(GetRequestParameter("detected"))));










                                    litMain.Text = "<PARCELID>" + intParcelId.ToString() + "<PARCELID>";
                                }
                                catch (Exception ex)
                                {
                                    litMain.Text = "<ERROR>" + ex.GetType().ToString() + "<br />" + ex.Message.ToString() + "<ERROR>";
                                }

                                break;
                            }
                        case "GetObjectId":
                            {
                                try
                                {
                                    int intObjectId = sql.GetInteger("clintonfrankland_db", "dbo", "spwtGetObjectId", new NamedValue("objectKey", GetRequestParameter("objectkey")), new NamedValue("objectName", GetRequestParameter("objectname")), new NamedValue("objectPosition", GetRequestParameter("objectposition")), new NamedValue("objectX", int.Parse(GetRequestParameter("objectx"))), new NamedValue("objectY", int.Parse(GetRequestParameter("objecty"))), new NamedValue("objectZ", int.Parse(GetRequestParameter("objectz"))), new NamedValue("ownerId", int.Parse(GetRequestParameter("ownerid"))), new NamedValue("parcelId", GetRequestParameter("parcelid")), new NamedValue("regionId", GetRequestParameter("regionid")), new NamedValue("objectUrl", GetRequestParameter("objecturl")), new NamedValue("detected", Convert.ToDateTime(GetRequestParameter("detected"))));










                                    litMain.Text = "<OBJECTID>" + intObjectId.ToString() + "<OBJECTID>";
                                }
                                catch (Exception ex)
                                {
                                    litMain.Text = "<ERROR>" + ex.GetType().ToString() + "<br />" + ex.Message.ToString() + "<ERROR>";
                                }

                                break;
                            }
                        case "UpdateTraffic":
                            {
                                try
                                {
                                    int intVisitResult = sql.GetInteger("clintonfrankland_db", "dbo", "spwtUpdateTraffic", new NamedValue("agentKey", GetRequestParameter("agentkey")), new NamedValue("agentName", GetRequestParameter("agentname")), new NamedValue("agentDisplayName", GetRequestParameter("agentdisplayname")), new NamedValue("agentPosition", GetRequestParameter("agentposition")), new NamedValue("agentLanguage", GetRequestParameter("agentlanguage")), new NamedValue("objectId", int.Parse(GetRequestParameter("objectid"))), new NamedValue("detected", Convert.ToDateTime(GetRequestParameter("detected"))));






                                    if (intVisitResult == 1)
                                    {
                                        litMain.Text = "<NEWVISIT>" + GetRequestParameter("agentkey") + "<NEWVISIT>";
                                    }
                                    else if (intVisitResult == 0)
                                    {
                                        litMain.Text = "<REPEATVISIT>" + GetRequestParameter("agentkey") + "<REPEATVISIT>";
                                    }
                                    else
                                    {
                                        litMain.Text = "";
                                    }
                                }
                                catch (Exception ex)
                                {
                                    litMain.Text = "<ERROR>" + ex.GetType().ToString() + "<br />" + ex.Message.ToString() + "<ERROR>";
                                }

                                break;
                            }

                        default:
                            {
                                litMain.Text = "<ERROR>Unknown command: " + Request["command"] + "<ERROR>";
                                break;
                            }
                    }
                }
            }
        }

        private string GetRequestParameter(string name)
        {
            if (Request[name] == null)
            {
                return "";
            }
            else
            {
                return Server.UrlDecode(Request[name]);
            }
        }

    }
}