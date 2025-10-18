<%@ Page Language="vb" AutoEventWireup="false" CodeBehind="Login.aspx.vb" Inherits="ClintonFrankland.Login" MasterPageFile="~/ClintonFrankland.Master" %>
<asp:Content ID="BodyContent" ContentPlaceHolderID="BodyContent" runat="server">
    <asp:HiddenField ID="hidReturn" runat="server" Value="" />
    <div class="container">
        <div class="mb-4"> <i class="fas fa-cogs fa-3x"></i></div>
        <h1 class="h3 mb-3 font-weight-normal">Sign in</h1>
        <div id="divWarning" runat="server" class="alert alert-warning" role="alert">Invalid username or password.</div>
        <div class="form-group">
            <label for="<%# inputUsername.ClientID %>" class="sr-only">Username</label>
            <asp:TextBox ID="inputUsername" runat="server" CssClass="form-control" placeholder="Username" required autofocus />
        </div>
        <div class="form-group">
            <label for="<%# inputPassword.ClientID %>" class="sr-only">Password</label>
            <asp:TextBox ID="inputPassword" runat="server" type="password" CssClass="form-control" placeholder="Password" required />
        </div>
        <asp:Button ID="btnSignin" runat="server" CssClass="btn btn-lg btn-primary btn-block" Text="Sign in" />
    </div>
</asp:Content>