<%@ Page Language="vb" AutoEventWireup="False" CodeBehind="../_Codebehind/SnapSignup.aspx.vb" Inherits="SnapDDA.SnapSignup" %>
<%@ Register TagPrefix="SnapControl" Namespace="SnapControls" Assembly="SnapDDA_WebControls"  %>
<script runat="server">
	Const PrivacyPolicyDocument As String = "/Clients/_MasterDocs/MasterPrivacyPolicy.pdf"
	
		'	For multimember and member searching
	Public subMember_count As Integer = 0
	Dim subpackage_count As Integer = 0

	Overloads Sub OnPreInit(ByVal sender As Object, ByVal e As EventArgs) Handles MyBase.PreInit
		DEBUGMODE = True
		DuplicateSubmissionErrorMessage = "<h2>Thank you</h2>" & _
		   "<p>Your membership has been processed and a confirmation email has been sent which contains all the details of your membership.</p>" & _
		   "<p>If you are having problems or do not receive the email, please contact us by email: NewMember@SnapDDA.co.uk <br>or call us on 01444 449166 (weekdays between 0900 and 1700)</p>"
		SnapConfiguration = ConfigSettings.LoadFromXML
		'ConfigSettingsFilename = "GenericClubSnapDDASignup"
		
		
		
	End Sub

	

	
	Overloads Sub OnPreLoad(ByVal sender As Object, ByVal e As EventArgs) Handles MyBase.PreLoad

		If Not GetSnapSession() Then
			Throw New ApplicationException("Sorry:  can't load Snap configuration!")
		End If
		
		' Use SagePay Test/validation servers:
		'Globals.URL_SagePayPrimary = "https://sandbox.opayo.eu.elavon.com/gateway/service/vspdirect-register.vsp"
		'Globals.URL_SagePaySecondary = "https://sandbox.opayo.eu.elavon.com/gateway/service/direct3dcallback.vsp"
		
	End Sub

	' The following is an override to fix an oversight in the codebehind.
	' Lets hope it gets merged into the codebase...
	Dim daysToFirstPayment As Integer

	Sub RecalculateUpfrontProRata
		If (SBP IsNot Nothing) AndAlso (SBP.Product IsNot Nothing) Then
			SBP.Product.UpFrontMembershipFeePence = SBP.DDCalculation.CalculateProRata(
				daysToFirstPayment,
				SBP.Product.MembershipStartDate,
				SBP.DDCalculation.CalculatedFirstPaymentDate,
				SBP.DDCalculation.TotalRecurringAmount,
				SBP.DDCalculation.CalculatedDailyRate,
				SBP.DDCalculation.PaymentIntervalMonths
			)

			SBP.DDCalculation.CalculatedDaysToFirstPayment = daysToFirstPayment
		End if
	End Sub

	Overloads Sub selStartDelay_SelectedIndexChanged(ByVal sender As Object, ByVal e As System.EventArgs) Handles selStartDelay.SelectedIndexChanged
		RecalculateUpfrontProRata()
	End Sub

</script>
<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml" lang="en">
	<head>
		<meta charset="utf-8" />
		<meta http-equiv="X-UA-Compatible" content="IE=Edge" />
		<meta name="viewport"  content="width=device-width, initial-scale=1.0, maximum-scale=1.5, user-scalable=1" />
		<!--	width=device-width, initial-scale=1, maximum-scale=1.5, user-scalable=1	-->
		<title>Xplorpay :: <%=Globals.ClubName %></title>
		<link href="https://fonts.googleapis.com/css2?family=Poppins:wght@700&display=swap" rel="stylesheet" />
		<link href="https://fonts.googleapis.com/css2?family=Roboto:wght@400&display=swap" rel="stylesheet">
		<link rel="Shortcut Icon" href="/favicon.ico" type="image/x-icon" />
		<link rel="stylesheet" href="../_CSS/GlobalStyle.css" />
		<link rel="stylesheet" href="ClubStyle.css?v=2" />
		
		<script type="text/javascript" src="/JS/jQuery/jquery-1.10.2.min.js"></script>
		<script type="text/javascript" src="/JS/H_PreventDoubleSubmissions.js"></script>
		<script type="text/javascript">
		Harlands.preventDups.ElementList.eleID = ['btnBankDetails_Next'];
		Harlands.preventDups.LB.eleLBID = ['LBMask'];
		Harlands.preventDups.LB.eleLBEnabledClass = ['LBShow'];
		</script>


	</head>
	<body>
		<% If PreviewWIPConfigMode Then %>
			<div id="PreviewInfo">
				Page running in Preview mode
			</div>
		<% End If %>

		<div id="TopBanner">
				<div>				
					<img alt="<%= Globals.ClubName %>" title="<%=Globals.ClubName %>" src="Images/Xplor_Pay_Logo_White.png" />
				</div>
			</div>
		
		<div id="MainContainer">
		
			
		   <form id="Signup" runat="server" enableviewstate="true" autocomplete="off">
		<% If (MultiView1.ActiveViewIndex = ViewPage.Finished) Then Response.Write("</form>") %>
				<div id="MainForm">
				
				<SnapControl:SnapSignupTabsControl_v2	runat="server" id="TabsControl1"
					P1_Branch = "Branch Selection"
					P1_Branch_Complete = "Branch Selection - Complete"
					P2_Product = "Membership Selection"
					P2_Product_Complete = "Membership Selection"
				/>

				<div id="CurrentView"><asp:MultiView id="MultiView1" runat="server" EnableViewState="true">
					<asp:View id="viewBranch" runat="server">
						<asp:ValidationSummary runat="server" id="valSum_Branch" ValidationGroup="valBranch"
							HeaderText="Please select the gym you would like to join, or all:"  CssClass="ErrorSummary" ShowMessageBox="False" />

						<p>
							Thanks for choosing <%= Globals.ClubName %>. We'll now take you through a few steps to sign you up.
							You'll need your bank account details to hand.
						</p>
						<p>
							These memberships are exclusively available on the internet and can only be paid
							by Direct Debit. If you wish to enquire about other membership options then please
							contact us directly at the club.
						</p>

					<% If Me.Branches.Count > 1 Then %>				
						<div class="FSWrap">
							<fieldset>
								<legend>Branch Selection</legend>
								<div>
									<p>
										<label for="BranchSelection">Branch Selection:</label>
										<em title="Mandatory field">*</em>
										<asp:DropDownList runat="server" ID="BranchSelection" CssClass="txtWide" EnableViewState="true" ValidationGroup="valBranch"  >
											<asp:listitem value="--" Text="-- Choose Membership"></asp:listitem>
										</asp:DropDownList>
										<asp:RequiredFieldValidator runat="server" InitialValue="--" id="rfv_BranchSelection" ControlToValidate="BranchSelection" SetFocusOnError="true"
											CssClass="Error" Display="Dynamic" ErrorMessage="Please select a branch" ValidationGroup="valBranch"></asp:RequiredFieldValidator>
									</p>

								</div>
							</fieldset>
						</div>
					<% End If %>

			<% If Me.Globals.UsePromoCodes Then %>
						<div class="FSWrap"><fieldset><legend>Promotional codes</legend><div>
							<p>
								If you have a promotional code,  please enter it here.<br />
								If you do not have a code, leave blank and press next step to continue.</p>
							<p>
								<label for="PromoCode">Promotional code:</label>
								<input runat="server" id="PromoCode"  class="txtMedium" />
							</p>
						</div></fieldset></div>
			<% End If %>

						<p class="Buttons">
							<asp:Button id="btnBranch_Next" runat="server" CssClass="btnNext" Text="Next step" ValidationGroup="valBranch"  />
						</p>
					</asp:View>

					<asp:View id="viewProduct" runat="server">
						<h2><asp:Literal ID="litSelectedBranchInProductSelection" runat="server" EnableViewState="false"  /></h2>
						<asp:ValidationSummary ID="valSum_Product" runat="server" CssClass="ErrorSummary"
							HeaderText="Please select the product you want:" ShowMessageBox="False"
							ValidationGroup="valProduct" />
						<h2>
							<asp:Literal runat="server" ID="litProductPageBranchInfo" EnableViewState="false"  />
							<asp:Literal runat="server" ID="litProductPageBranchInfo2" EnableViewState="false"  />
						</h2>
						<div class="FSWrap noBG"><fieldset><legend>Membership Selection</legend><div>
							<p>
								<asp:RadioButtonList runat="server" id="ProductSelection" RepeatLayout="Flow" EnableViewState="true"
									EnableTheming="False" RepeatColumns="1" RepeatDirection="Horizontal" ValidationGroup="valProduct" >
								</asp:RadioButtonList>
								<asp:RequiredFieldValidator ID="rfv_ProductSelection" runat="server" ControlToValidate="ProductSelection"
									CssClass="WideError" Display="Dynamic" ErrorMessage="Please select a product" SetFocusOnError="true"
									ValidationGroup="valProduct"></asp:RequiredFieldValidator>
							</p>
						</div></fieldset></div>

			<% If (Globals.DeferredMembership And Me.SBP.Branch.DeferredMembership <> 0) Or Me.SBP.Branch.DeferredMembership = 1 Then %>
						<div class="FSWrap">
							<fieldset>
								<legend>Delayed membership</legend>
								<div>
									<p>
										<label for="BranchSelection" class="WideLabel">Do you wish to delay the start of your membership because
										you have an existing gym contract?</label>
										<select id="DeferPaymentMonths" runat="server" class="WideSelect" enableviewstate="true" name="DeferPaymentMonths">
											<option value="0">No delay, join me immediately</option>
											<option value="1">1 month delay</option>
											<option value="2">2 months delay</option>
											<option value="3">3 months delay</option>
										</select>
									</p>
								</div>
							</fieldset>
						</div>
			<% End If %>

						<p class="Buttons">
							<asp:Button id="btnProduct_Next" runat="server" CssClass="btnNext" Text="Next step" ValidationGroup="valProduct"  />
							<asp:Button id="btnProduct_Prev" runat="server" CssClass="btnSecondary" Text="Previous step" ValidationGroup="valProduct" CausesValidation="false" />
						</p>
						<script type="text/javascript" src="/JS/H_ProductGrouping.js"></script>
					</asp:View>

					<asp:View id="viewAddOns" runat="server">
						<div id="AddOns">
							<h2><asp:Literal ID="litSelectedBranchProductInAddonSelection" runat="server" EnableViewState="false"  /></h2>
							<asp:ValidationSummary ID="valSum_Addons" runat="server" CssClass="ErrorSummary"
								HeaderText="Please correct the following errors:" ShowMessageBox="True" ValidationGroup="valAddons" />

							<asp:Literal runat="server" ID="litAddonPageProductInfo" />

							<div class="FSWrap"><fieldset><legend>Optional extras</legend><div>

							<SnapControl:AddOnsControl runat="server" id="ctlAddOns" EnableViewState="true" />

							<asp:Literal runat="server" id="litAddons" />

							</div></fieldset></div>

							<asp:Literal runat="server" ID="litAddonPageProductInfo2" />

							<p class="Buttons">
								<asp:Button ID="btnAddons_Next" runat="server" CssClass="btnNext" Text="Next step" ValidationGroup="valAddons" CausesValidation="true" />
								<asp:Button id="btnAddons_Prev" runat="server" CssClass="btnSecondary" Text="Previous step" />
							</p>
						</div>
					</asp:View>

					<asp:View id="viewPersonalInfo" runat="server">
						<asp:Button id="btnShowOverrideForm" runat="server" CssClass="btnNext" Text="Change product details" CausesValidation="false" Visible="false"  />
						<asp:ValidationSummary runat="server" id="valSum_PersonalInfo" ValidationGroup="valPersonalInfo"
							HeaderText="Please correct the following errors:"  CssClass="ErrorSummary" ShowMessageBox="True" />
							
	<% If SBP.Product.UpFrontCollection = True then %>
						<div class="FSWrap"><fieldset><legend>In-Club payments</legend><div>
							<p>These payments are taken in club - not by Direct Debit:</p>
							
		<% If SBP.Product.UpFrontJoiningFeePence > 0 Then %>
							<p>
								<span class="Label">Joining fee:</span>
								<span>&pound;<%=Format(SBP.Product.UpFrontJoiningFeePence / 100, "0.00")%></span>
							</p>
		<% End If %>
		
		<% If SBP.Product.UpFrontMembershipFeePence > 0 Then %>
		
			<% If SBP.Product.UpfrontProRataPayment = True %>
							<p>
								<span class="Label">Pro-rata fee*:</span>
								<span>&pound;<%=Format(SBP.Product.UpFrontMembershipFeePence / 100, "0.00")%></span>
							</p>
							<p>*Pro-rata fees are for the <%=SBP.DDCalculation.CalculatedDaysToFirstPayment %> days membership between the 
								Membership Start Date on <%=Format(SBP.Product.MembershipStartDate, "dd MMM yyy")%>
								and the First Direct Debit collection on <%=Format(SBP.DDCalculation.CalculatedFirstPaymentDate, "dd MMM yyy")%>
							</p>
			<% Else %>
							<p>
								<span class="Label">Membership fee:</span>
								<span>&pound;<%=Format(SBP.Product.UpFrontMembershipFeePence / 100, "0.00")%></span>
							</p>
			<% End If %>
			
		<% End If %>
						</div></fieldset></div>
	<% End If %>
		
						<div class="FSWrap">
							<fieldset>
								<legend>About your membership</legend>
								<div>
									<% If SBP.Product.SecondPaymentDefermentMonths > 0 Then %>
										<p>
											You have elected to defer your Membership start date by <%=SBP.Product.SecondPaymentDefermentMonths%> months
											until <%=Format(Now().AddMonths(SBP.Product.SecondPaymentDefermentMonths), "dd MMM yyyy")%>
										</p>
									<% End If %>
									
									<asp:Literal runat="server" ID="litPersonalInfoPaymentDetails" />
								</div>
							</fieldset>
						</div>

						<div class="FSWrap"><fieldset><legend>About you</legend><div>
							<p>
								<label for="Title">Title:</label>
								<em title="Mandatory field">*</em>
								<select runat="server" id="Title" class="txtMedium">
									<option value="" selected="selected">-- Choose</option>
									<option value="Mr">Mr</option>
									<option value="Mrs">Mrs</option>
									<option value="Miss">Miss</option>
									<option value="Other">Other --&gt;</option>
								</select>
								<input id="TitleOther" runat="server" class="txtMedium" type="text" title="Title other" />
								<asp:RequiredFieldValidator runat="server" id="vTitle" ControlToValidate="Title" SetFocusOnError="true"
									CssClass="Error" Display="Dynamic" ErrorMessage="Title not selected" ValidationGroup="valPersonalInfo">Title must be selected</asp:RequiredFieldValidator>
							</p>
							<p>
								<label for="Forename">Forename:</label>
								<em title="Mandatory field">*</em>
								<input runat="server" id="Forename" class="txtWide"  placeholder="Forename" />
								<asp:RequiredFieldValidator id="rfv_Forename" runat="server" ControlToValidate="Forename" SetFocusOnError="true"
									CssClass="Error" Display="Dynamic" ValidationGroup="valPersonalInfo"
									ErrorMessage="Forename not entered">Forename cannot be blank</asp:RequiredFieldValidator>
								<asp:RegularExpressionValidator id="vxForename" runat="server"  SetFocusOnError="true" ControlToValidate="Forename"
									ErrorMessage="Forename contains invalid characters"
									 CssClass="Error" Display="Dynamic" ValidationGroup="valPersonalInfo"
									ValidationExpression="^[A-Za-z\'\- ]{2,}$">Invalid characters in forename</asp:RegularExpressionValidator>
							</p>
							<p>
								<label for="Surname">Surname:</label>
								<em title="Mandatory field">*</em>
								<input runat="server" id="Surname" class="txtWide"  placeholder="Surname" />
								<asp:RequiredFieldValidator id="RequiredFieldValidator1" runat="server" ControlToValidate="Surname" SetFocusOnError="true"
									CssClass="Error" Display="Dynamic" ValidationGroup="valPersonalInfo"
									ErrorMessage="Surname not entered">Surname cannot be blank</asp:RequiredFieldValidator>
								<asp:RegularExpressionValidator id="RegularExpressionValidator1" runat="server" ControlToValidate="Surname" SetFocusOnError="true"
									ErrorMessage="Surname contains invalid characters"
									CssClass="Error" Display="Dynamic" ValidationGroup="valPersonalInfo"
									ValidationExpression="^[A-za-z\'\- ]{2,}$">Invalid characters in surname</asp:RegularExpressionValidator>
							</p>
							<p>
								<span class="Label">Gender:</span>
								<em title="Mandatory field">*</em>
								<span class="RadioCheckLabel">
									<asp:RadioButtonList runat="server" id="Gender" RepeatLayout="Flow" EnableTheming="False" RepeatColumns="2" RepeatDirection="Horizontal" ValidationGroup="valPersonalInfo" >
										<asp:ListItem Value="male">Male</asp:ListItem>
										<asp:ListItem Value="female">Female</asp:ListItem>
									</asp:RadioButtonList>
								</span>
								<asp:RequiredFieldValidator id="RequiredFieldValidator2" runat="server" ControlToValidate="Gender" SetFocusOnError="true"
									CssClass="Error" Display="Dynamic" ValidationGroup="valPersonalInfo"
									ErrorMessage="Select your gender">Select your gender</asp:RequiredFieldValidator>
							</p>
							<p>
								<label>Date of birth <i>(must be over <%=CStr(Me.SBP.Product.YoungAgeLimitYears)%>)</i>:</label>
								<em title="Mandatory field">*</em>
								<select runat="server" id="dob_d" class="txtNarrow">
									<option  value="" selected="selected">DD</option>
									<option value="1">1</option>
									<option value="2">2</option>
									<option value="3">3</option>
									<option value="4">4</option>
									<option value="5">5</option>
									<option value="6">6</option>
									<option value="7">7</option>
									<option value="8">8</option>
									<option value="9">9</option>
									<option value="10">10</option>
									<option value="11">11</option>
									<option value="12">12</option>
									<option value="13">13</option>
									<option value="14">14</option>
									<option value="15">15</option>
									<option value="16">16</option>
									<option value="17">17</option>
									<option value="18">18</option>
									<option value="19">19</option>
									<option value="20">20</option>
									<option value="21">21</option>
									<option value="22">22</option>
									<option value="23">23</option>
									<option value="24">24</option>
									<option value="25">25</option>
									<option value="26">26</option>
									<option value="27">27</option>
									<option value="28">28</option>
									<option value="29">29</option>
									<option value="30">30</option>
									<option value="31">31</option>
								</select>
								<select runat="server"	id="dob_m" class="txtNarrow">
									<option value="" selected="selected">MM</option>
									<option value="1">Jan</option>
									<option value="2">Feb</option>
									<option value="3">Mar</option>
									<option value="4">Apr</option>
									<option value="5">May</option>
									<option value="6">Jun</option>
									<option value="7">Jul</option>
									<option value="8">Aug</option>
									<option value="9">Sep</option>
									<option value="10">Oct</option>
									<option value="11">Nov</option>
									<option value="12">Dec</option>
								</select>
								<asp:DropDownList runat="server" id="dob_y" CssClass="txtNarrow Last"/>
								<asp:RequiredFieldValidator ID="rvf_dob_d" runat="server" ControlToValidate="dob_d"
									CssClass="Error" Display="Dynamic" ErrorMessage="Day of birth not specified"
									SetFocusOnError="true" ValidationGroup="valPersonalInfo">Select your birth day</asp:RequiredFieldValidator>
								<asp:RequiredFieldValidator ID="rvf_dob_m" runat="server" ControlToValidate="dob_m"
									CssClass="Error" Display="Dynamic" ErrorMessage="Month of birth not specified"
									SetFocusOnError="true" ValidationGroup="valPersonalInfo">Select your month of birth</asp:RequiredFieldValidator>
								<asp:RequiredFieldValidator ID="rvf_dob_y" runat="server" ControlToValidate="dob_y"
									CssClass="Error" Display="Dynamic" ErrorMessage="Year of birth not specified"
									SetFocusOnError="true" ValidationGroup="valPersonalInfo">Select your year of birth</asp:RequiredFieldValidator>
							</p>
			<% If Globals.ShowMembershipNumberField Then%>
							<p>
								<label for="MembershipNo" title="Enter the club's membership number">Club membership number:</label>
								<em title="Mandatory field">*</em>
								<input runat="server" id="MembershipNo" class="txtMedium" />
							</p>
			<% End If%>
						<!--<p>
							<label for="EmployeeNumber">Employee Number</label>
							<input runat="server" id="employeenumber" class="txtWide" placeholder="Employee Number" />
						</p>-->
						
<script type="text/javascript">

				function show() {
		var option = document.getElementById("Relation").value;
            if(option == "Self") {
		document.getElementById('M1_Title').value = document.getElementById('Title').value;
        document.getElementById('M1_Forename').value = document.getElementById('Forename').value;
		document.getElementById('M1_Surname').value = document.getElementById('Surname').value;
		document.getElementById('M1_Gender').value = document.getElementById('Gender').value;
}
		else {				
}
        }
</script>
						</div></fieldset></div>

						<div class="FSWrap"><fieldset><legend>Your contact details</legend><div>
							<p>
								<label for="MobilePhone">Mobile phone:</label>
								<em title="Mandatory field">*</em>
								<input runat="server" id="MobilePhone" class="txtWide" placeholder="Mobile phone" />
								<asp:RequiredFieldValidator ID="rvf_MobilePhone" runat="server" ControlToValidate="MobilePhone"
									CssClass="Error" Display="Dynamic" ErrorMessage="Mobile phone number blank" SetFocusOnError="true"
									ValidationGroup="valPersonalInfo">Mobile phone cannot be blank</asp:RequiredFieldValidator>
								<asp:RegularExpressionValidator ID="rvf_re_MobilePhone" runat="server" ControlToValidate="MobilePhone"
									CssClass="Error" Display="Dynamic" ErrorMessage="Mobile phone contains invalid characters"
									SetFocusOnError="true" ValidationExpression="^[0-9\- \(\)]{7,}$" ValidationGroup="valPersonalInfo">Contains invalid characters</asp:RegularExpressionValidator>
							</p>
							<p>
								<label for="HomePhone">Home telephone number:</label>
								<input runat="server" id="HomePhone" class="txtWide" placeholder="Home phone" />
								<asp:RegularExpressionValidator ID="rvf_re_HomePhone" runat="server" ControlToValidate="HomePhone"
									CssClass="Error" Display="Dynamic" ErrorMessage="Home phone contains invalid characters"
									SetFocusOnError="true" ValidationExpression="^[0-9\- \(\)]{7,}$" ValidationGroup="valPersonalInfo">Contains invalid characters</asp:RegularExpressionValidator>
							</p>
							<p>
								<label for="Email">Email:</label>
								<em title="Mandatory field">*</em>
								<input runat="server" id="Email" class="txtWide" placeholder="Email address" />
								<asp:RequiredFieldValidator ID="rvf_Email" runat="server" ControlToValidate="Email"
									CssClass="Error" Display="Dynamic" ErrorMessage="Email address must be provided"
									SetFocusOnError="true" ValidationGroup="valPersonalInfo">Email address must be provided</asp:RequiredFieldValidator>
								<asp:RegularExpressionValidator ID="rvf_re_Email" runat="server" ControlToValidate="Email"
									CssClass="Error" Display="Dynamic" ErrorMessage="Invalid email address" SetFocusOnError="true"
									ValidationExpression="^[\w_\.\-]+?@[A-Za-z0-9\-\.]{2,}?\.((\w{2})|(aero|asia|biz|cat|com|coop|edu|gov|info|int|jobs|mil|mobi|museum|name|net|org|pro|tel|travel))$"
									ValidationGroup="valPersonalInfo">Invalid email address</asp:RegularExpressionValidator>
							</p>
							<p>
								<label for="EmailConfirm">Please confirm your email address:</label>
								<em title="Mandatory field">*</em>
								<input runat="server" id="EmailConfirm" class="txtWide" placeholder="Confirm email addr." />
								<asp:CompareValidator runat="server" ControlToCompare="Email" ControlToValidate="EmailConfirm"
									CssClass="Error" Display="dynamic" ErrorMessage="Email addresses don't match"
									SetFocusOnError="true">Email addresses don't match</asp:CompareValidator>
							</p>
			<% If Me.Globals.ShowEmployerNameField Then %>
							<p>
								<label for="EmployersName">
									Your employers name:</label>
								<input id="EmployersName" runat="server" class="txtWide" />
							</p>
			<% End If %>
			<!--
							<p class="FullWidthCheckbox">
								<input type="checkbox" runat="server" id="MarketingByEmail" />
								<label for="MarketingByEmail">From time to time, we may want to send you
									information by email. Please tick this box if you would like to receive
									this information by email.</label>
							</p>
			-->
						</div></fieldset></div>

			<% If Me.SBP.Product.JointMembership Then %>
						<div class="FSWrap"><fieldset><legend>Joint Member's Details</legend><div>
							<p>
								<label for="JointMemberTitle">Joint member's title:</label>
								<em title="Mandatory field">*</em>
								<select id="JointMemberTitle" runat="server" class="txtMedium">
									<option value="" selected="selected">-- Choose</option>
									<option value="Mr">Mr</option>
									<option value="Mrs">Mrs</option>
									<option value="Miss">Miss</option>
									<option value="Other">Other --&gt;</option>
								</select>
								<input id="JointMemberTitleOther" runat="server" class="txtMedium" type="text" />
								<asp:RequiredFieldValidator ID="rvf_JointMemberTitle" runat="server" ControlToValidate="JointMemberTitle"
									CssClass="Error" Display="Dynamic" ErrorMessage="Joint member's title not selected"
									SetFocusOnError="true" ValidationGroup="valPersonalInfo">Joint member's title must be entered</asp:RequiredFieldValidator>
							</p>
							<p>
								<label for="JointMemberForename">Joint member's forename:</label>
								<em title="Mandatory field">*</em>
								<input id="JointMemberForename" runat="server" class="txtWide" placeholder="Forename" />
								<asp:RequiredFieldValidator ID="rvf_JointMemberForename" runat="server" ControlToValidate="JointMemberForename"
									CssClass="Error" Display="Dynamic" ErrorMessage="Joint member forename not entered"
									SetFocusOnError="true" ValidationGroup="valPersonalInfo">Joint member's forename cannot be blank</asp:RequiredFieldValidator>
								<asp:RegularExpressionValidator ID="rvf_re_JointMemberForename" runat="server"
									ControlToValidate="JointMemberForename" CssClass="Error" Display="Dynamic" ErrorMessage="Joint member forename contains invalid characters"
									SetFocusOnError="true" ValidationExpression="^[A-za-z\'\- ]{2,}$" ValidationGroup="valPersonalInfo">Joint member's forename contains invalid characters</asp:RegularExpressionValidator>
							</p>
							<p>
								<label for="JointMemberSurname">Joint member's surname:</label>
								<em title="Mandatory field">*</em>
								<input id="JointMemberSurname" runat="server" class="txtWide" placeholder="Surname" />
								<asp:RequiredFieldValidator ID="rvf_JointMemberSurname" runat="server" ControlToValidate="JointMemberSurname"
									CssClass="Error" Display="Dynamic" ErrorMessage="Surname not entered" SetFocusOnError="true"
									ValidationGroup="valPersonalInfo">Joint member's surname cannot be blank</asp:RequiredFieldValidator>
								<asp:RegularExpressionValidator ID="rvf_re_JointMemberSurname" runat="server" ControlToValidate="JointMemberSurname"
									CssClass="Error" Display="Dynamic" ErrorMessage="Joint member surname contains invalid characters"
									SetFocusOnError="true" ValidationExpression="^[A-za-z\'\- ]{2,}$" ValidationGroup="valPersonalInfo">Joint member's surname contains invalid characters</asp:RegularExpressionValidator>
							</p>
							<p>
								<span class="Label">Joint member's gender:</span>
								<em title="Mandatory field">*</em>
								<span class="RadioCheckLabel">
									<asp:RadioButtonList runat="server" id="JointMemberGender" RepeatLayout="Flow" EnableTheming="False" RepeatColumns="2" RepeatDirection="Horizontal" ValidationGroup="valPersonalInfo" >
										<asp:ListItem Value="male">Male</asp:ListItem>
										<asp:ListItem Value="female">Female</asp:ListItem>
									</asp:RadioButtonList>
								</span>
								<asp:RequiredFieldValidator id="rfv_JointMemberGender" runat="server" ControlToValidate="JointMemberGender" SetFocusOnError="true"
									CssClass="Error" Display="Dynamic" ValidationGroup="valPersonalInfo"
									ErrorMessage="Select the joint member's gender">Select the joint member's gender</asp:RequiredFieldValidator>
							</p>
							<p>
								<label for="DateOfBirth">
									Joint member's date of birth<br /><i>(must be over <%=Me.SBP.Product.YoungAgeLimitYears%>)</i>:</label>
								<em title="Mandatory field">*</em>
								<select runat="server" id="JointMemberDob_d" class="txtNarrow">
									<option selected="selected" value="">DD</option>
									<option value="1">1</option>
									<option value="2">2</option>
									<option value="3">3</option>
									<option value="4">4</option>
									<option value="5">5</option>
									<option value="6">6</option>
									<option value="7">7</option>
									<option value="8">8</option>
									<option value="9">9</option>
									<option value="10">10</option>
									<option value="11">11</option>
									<option value="12">12</option>
									<option value="13">13</option>
									<option value="14">14</option>
									<option value="15">15</option>
									<option value="16">16</option>
									<option value="17">17</option>
									<option value="18">18</option>
									<option value="19">19</option>
									<option value="20">20</option>
									<option value="21">21</option>
									<option value="22">22</option>
									<option value="23">23</option>
									<option value="24">24</option>
									<option value="25">25</option>
									<option value="26">26</option>
									<option value="27">27</option>
									<option value="28">28</option>
									<option value="29">29</option>
									<option value="30">30</option>
									<option value="31">31</option>
								</select>
								<select runat="server"	id="JointMemberDob_m" class="txtNarrow">
									<option selected="selected" value="">MM</option>
									<option value="1">Jan</option>
									<option value="2">Feb</option>
									<option value="3">Mar</option>
									<option value="4">Apr</option>
									<option value="5">May</option>
									<option value="6">Jun</option>
									<option value="7">Jul</option>
									<option value="8">Aug</option>
									<option value="9">Sep</option>
									<option value="10">Oct</option>
									<option value="11">Nov</option>
									<option value="12">Dec</option>
								</select>
								<asp:DropDownList runat="server" id="JointMemberDob_y" CssClass="txtNarrow Last" />
								<asp:RequiredFieldValidator ID="rvf_JointMemberDob_d" runat="server" ControlToValidate="JointMemberDob_d"
									CssClass="Error" Display="Dynamic" ErrorMessage="Joint member's day of birth not specified"
									SetFocusOnError="true" ValidationGroup="valPersonalInfo">Select joint member's birth day</asp:RequiredFieldValidator>
								<asp:RequiredFieldValidator ID="rvf_JointMemberDob_m" runat="server" ControlToValidate="JointMemberDob_m"
									CssClass="Error" Display="Dynamic" ErrorMessage="Joint member's month of birth not specified"
									SetFocusOnError="true" ValidationGroup="valPersonalInfo">Select joint member's month of birth</asp:RequiredFieldValidator>
								<asp:RequiredFieldValidator ID="rvf_JointMemberDob_y" runat="server" ControlToValidate="JointMemberDob_y"
									CssClass="Error" Display="Dynamic" ErrorMessage="Joint member's year of birth not specified"
									SetFocusOnError="true" ValidationGroup="valPersonalInfo">Select joint member's year of birth</asp:RequiredFieldValidator>
							</p>
				<% If Globals.ShowMembershipNumberField Then %>
							<p>
								<label for="JointMembershipNo" title="Enter the club's membership number for the joint member">Joint member's club membership number:</label>
								<em title="Mandatory field">*</em>
								<input runat="server" id="JointMembershipNo" class="txtMedium" />
							</p>
				<% End If %>
						</div></fieldset></div>
			<% End If %>
			
			<% 						

	'	 response.write("SBP.Product.SubMemberCount = " & SBP.Product.SubMemberCount)

	'	SubMemberCount determines the number of SubMember rows
	If (SBP.Product.SubMemberCount > 0) Then

		If (IsNumeric(SBP.Product.SubMemberCount)) Then subMember_count = CInt(SBP.Product.SubMemberCount)

		'	Extract the description...
		'	Fixed format packages...  These OVERRIDE variable packages.
		'	Need one per sub-member.  The description can contain HTML (URL encoded)
		'		<submember1_packagedescription>Adult member 1</submember1_packagedescription>
		'		<submember1_packagecode></submember1_packagecode>				<!-- leaving this blank means "take the Prime member's package code" -->
		'		<submember2_packagedescription>Child member 1</submember2_packagedescription>
		'		<submember2_packagecode>5527</submember2_packagecode>
		'		.....



%>
				<div class="FSWrap">
					<fieldset>
						<legend>Additional Member's Details</legend>
						<div>
<!--
						<table id="FamilyMembersTable">
							<tr>
								<th class="colTitle">Title</th>
								<th class="colForename">Forename</th>
								<th class="colSurname">Surname</th>
								<th class="colGender">Gender</th>
								<th class="colDOB">DOB</th>
							</tr>
-->					
<%					
				If (subMember_count >= 1) Then
%>
							<h3 class="familyMemberRow">
								Child Member 1 
							</h3>
							<p>
								<label for="M1_Title">Title:</label>
								<select runat="server" id="M1_Title" class="txtMedium">
									<option selected="selected" value="">Title</option>
									<option value="Master">Master</option>
									<option value="Miss">Miss</option>
									<option value="Mr">Mr</option>
									<option value="Mrs">Mrs</option>
									<option value="Ms">Ms</option>
								</select>
							</p>
							<p>
								<label for="M1_Forename">Forename:</label>
								<input id="M1_Forename" runat="server" class="txtWide" placeholder="Forename" />
							</p>
							<p>
								<label for="M1_Surname">Surname:</label>
								<input id="M1_Surname" runat="server" class="txtWide" placeholder="Surname" />
							</p>
							<p>
								<label>Gender:</label>
								<span class="RadioCheckLabel">
									<asp:RadioButtonList runat="server" id="M1_Gender" RepeatLayout="Flow" EnableTheming="False" RepeatColumns="2" RepeatDirection="Horizontal" ValidationGroup="valPersonalInfo" >
										<asp:ListItem Value="male">Male</asp:ListItem>
										<asp:ListItem Value="female">Female</asp:ListItem>
									</asp:RadioButtonList>
								</span>
									
<%
'Save for posterity....  This was the plan for using the 
'							<td><select runat="server" id="M1_SubPackageCode" class="txtMedium" alt="Select the product for this person"></select></td>
%>
							</p>
							<p>
								<label for="M1_DOB">Date of Birth <nobr>(dd/mm/yyyy):</nobr></label>
								<input id="M1_DOB" runat="server" class="txtMedium" placeholder="dd/mm/yyyy" />
							</p>
		
							<p>
								<asp:RegularExpressionValidator ID="rvf_re_M1_Forename" runat="server"
									ControlToValidate="M1_Forename" CssClass="Error" Display="Dynamic" ErrorMessage="Forename1 contains invalid characters"
									SetFocusOnError="true" ValidationExpression="^[A-za-z\'\- ]{2,}$" ValidationGroup="valPersonalInfo">Member 1's Forename contains invalid characters</asp:RegularExpressionValidator>
								<asp:RegularExpressionValidator ID="rvf_re_M1_Surname" runat="server" ControlToValidate="M1_Surname"
											CssClass="Error" Display="Dynamic" ErrorMessage="Surname contains invalid characters"
											SetFocusOnError="true" ValidationExpression="^[A-za-z\'\- ]{2,}$" ValidationGroup="valPersonalInfo">Member 1's Surname contains invalid characters</asp:RegularExpressionValidator>	
								<asp:RequiredFieldValidator id="rfv_M1_Title" runat="server" ControlToValidate="M1_Title" SetFocusOnError="true"
											CssClass="Error" Display="Dynamic" ValidationGroup="valPersonalInfo"
											ErrorMessage="Sub-member 1 title not selected">Select the title</asp:RequiredFieldValidator>
								<asp:RequiredFieldValidator id="rfv_M1_Forename" runat="server" ControlToValidate="M1_Forename" SetFocusOnError="true"
											CssClass="Error" Display="Dynamic" ValidationGroup="valPersonalInfo"
											ErrorMessage="Sub-member 1 forename not entered">Forename cannot be blank</asp:RequiredFieldValidator>
								<asp:RequiredFieldValidator id="rfv_M1_Surname" runat="server" ControlToValidate="M1_Surname" SetFocusOnError="true"
											CssClass="Error" Display="Dynamic" ValidationGroup="valPersonalInfo"
											ErrorMessage="Sub-member 1 surname not entered">Surname cannot be blank</asp:RequiredFieldValidator>
								<asp:RequiredFieldValidator id="rfv_M1_Gender" runat="server" ControlToValidate="M1_Gender" SetFocusOnError="true"
											CssClass="Error" Display="Dynamic" ValidationGroup="valPersonalInfo"
											ErrorMessage="Sub-member 1 gender not selected">Select the gender</asp:RequiredFieldValidator>
								<asp:RequiredFieldValidator id="rfv_M1_DOB" runat="server" ControlToValidate="M1_DOB" SetFocusOnError="true"
											CssClass="Error" Display="Dynamic" ValidationGroup="valPersonalInfo"
											ErrorMessage="Sub-member 1 DoB not entered">DoB cannot be blank</asp:RequiredFieldValidator>
								<asp:RegularExpressionValidator ID="rvf_re_M1_DOB" runat="server"
											ControlToValidate="M1_DOB" CssClass="Error" Display="Dynamic" ErrorMessage="DOB1 contains invalid characters"
											SetFocusOnError="true" ValidationExpression="[0123]?\d\/[01]\d\/[12]\d\d\d" ValidationGroup="valPersonalInfo">DOB should be a valid birth date 'dd/mm/yyyy', e.g. 01/01/1969</asp:RegularExpressionValidator>
							</p>
<%
				End If

				If (subMember_count >= 2) Then

%>
							<h3 class="familyMemberRow lineAbove">
								Child Member 2 

							</h3>
							<p>
								<label for="M2_Title">Title:</label>
								<select runat="server" id="M2_Title" class="txtMedium">
									<option selected="selected" value="">Title</option>
									<option value="master">Master</option>
									<option value="miss">Miss</option>
									<option value="mr">Mr</option>
									<option value="mrs">Mrs</option>
									<option value="ms">Ms</option>
								</select>
							</p>
							<p>
								<label for="M2_Forename">Forename:</label>
								<input id="M2_Forename" runat="server" class="txtWide" placeholder="Forename" />
							</p>
							<p>
								<label for="M2_Surname">Surname:</label>
								<input id="M2_Surname" runat="server" class="txtWide" placeholder="Surname" />
							</p>
							<p>
								<label>Gender:</label>
								<span class="RadioCheckLabel">
									<asp:RadioButtonList runat="server" id="M2_Gender" RepeatLayout="Flow" EnableTheming="False" RepeatColumns="2" RepeatDirection="Horizontal" ValidationGroup="valPersonalInfo" >
										<asp:ListItem Value="male">Male</asp:ListItem>
										<asp:ListItem Value="female">Female</asp:ListItem>
									</asp:RadioButtonList>
								</span>
									
<%
'Save for posterity....  This was the plan for using the 
'							<td><select runat="server" id="M2_SubPackageCode" class="txtMedium" alt="Select the product for this person"></select></td>
%>
							</p>
							<p>
								<label for="M2_DOB">Date of Birth <nobr>(dd/mm/yyyy):</nobr></label>
								<input id="M2_DOB" runat="server" class="txtMedium" placeholder="dd/mm/yyyy" />
							</p>
							<p>
								<asp:RegularExpressionValidator ID="rvf_re_M2_Forename" runat="server"
									ControlToValidate="M2_Forename" CssClass="Error" Display="Dynamic" ErrorMessage="Forename1 contains invalid characters"
									SetFocusOnError="true" ValidationExpression="^[A-za-z\'\- ]{2,}$" ValidationGroup="valPersonalInfo">Member 1's Forename contains invalid characters</asp:RegularExpressionValidator>
								<asp:RegularExpressionValidator ID="rvf_re_M2_Surname" runat="server" ControlToValidate="M2_Surname"
											CssClass="Error" Display="Dynamic" ErrorMessage="Surname contains invalid characters"
											SetFocusOnError="true" ValidationExpression="^[A-za-z\'\- ]{2,}$" ValidationGroup="valPersonalInfo">Member 1's Surname contains invalid characters</asp:RegularExpressionValidator>	
								<asp:RequiredFieldValidator id="rfv_M2_Title" runat="server" ControlToValidate="M2_Title" SetFocusOnError="true"
											CssClass="Error" Display="Dynamic" ValidationGroup="valPersonalInfo"
											ErrorMessage="Sub-member 2 title not selected">Select the title</asp:RequiredFieldValidator>
								<asp:RequiredFieldValidator id="rfv_M2_Forename" runat="server" ControlToValidate="M2_Forename" SetFocusOnError="true"
											CssClass="Error" Display="Dynamic" ValidationGroup="valPersonalInfo"
											ErrorMessage="Sub-member 2 forename not entered">Forename cannot be blank</asp:RequiredFieldValidator>
								<asp:RequiredFieldValidator id="rfv_M2_Surname" runat="server" ControlToValidate="M2_Surname" SetFocusOnError="true"
											CssClass="Error" Display="Dynamic" ValidationGroup="valPersonalInfo"
											ErrorMessage="Sub-member 2 surname not entered">Surname cannot be blank</asp:RequiredFieldValidator>
								<asp:RequiredFieldValidator id="rfv_M2_Gender" runat="server" ControlToValidate="M2_Gender" SetFocusOnError="true"
											CssClass="Error" Display="Dynamic" ValidationGroup="valPersonalInfo"
											ErrorMessage="Sub-member 2 gender not selected">Select the gender</asp:RequiredFieldValidator>
								<asp:RequiredFieldValidator id="rfv_M2_DOB" runat="server" ControlToValidate="M2_DOB" SetFocusOnError="true"
											CssClass="Error" Display="Dynamic" ValidationGroup="valPersonalInfo"
											ErrorMessage="Sub-member 2 DoB not entered">DoB cannot be blank</asp:RequiredFieldValidator>
								<asp:RegularExpressionValidator ID="rvf_re_M2_DOB" runat="server"
											ControlToValidate="M2_DOB" CssClass="Error" Display="Dynamic" ErrorMessage="DOB1 contains invalid characters"
											SetFocusOnError="true" ValidationExpression="[0123]?\d\/[01]\d\/[12]\d\d\d" ValidationGroup="valPersonalInfo">DOB should be a valid birth date 'dd/mm/yyyy', e.g. 01/01/1969</asp:RegularExpressionValidator>
							</p>
<%


				End If

				If (subMember_count >= 3) Then

%>
							<h3 class="familyMemberRow lineAbove">
								Child Member 3  
							
							</h3>
							<p>
								<label for="M3_Title">Title:</label>
								<select runat="server" id="M3_Title" class="txtMedium">
									<option selected="selected" value="">Title</option>
									<option value="master">Master</option>
									<option value="miss">Miss</option>
									<option value="mr">Mr</option>
									<option value="mrs">Mrs</option>
									<option value="ms">Ms</option>
								</select>
							</p>
							<p>
								<label for="M3_Forename">Forename:</label>
								<input id="M3_Forename" runat="server" class="txtWide" placeholder="Forename" />
							</p>
							<p>
								<label for="M3_Surname">Surname:</label>
								<input id="M3_Surname" runat="server" class="txtWide" placeholder="Surname" />
							</p>
							<p>
								<label>Gender:</label>
								<span class="RadioCheckLabel">
									<asp:RadioButtonList runat="server" id="M3_Gender" RepeatLayout="Flow" EnableTheming="False" RepeatColumns="2" RepeatDirection="Horizontal" ValidationGroup="valPersonalInfo" >
										<asp:ListItem Value="male">Male</asp:ListItem>
										<asp:ListItem Value="female">Female</asp:ListItem>
									</asp:RadioButtonList>
								</span>
									
<%
'Save for posterity....  This was the plan for using the 
'							<td><select runat="server" id="M3_SubPackageCode" class="txtMedium" alt="Select the product for this person"></select></td>
%>
							</p>
							<p>
								<label for="M3_DOB">Date of Birth <nobr>(dd/mm/yyyy):</nobr></label>
								<input id="M3_DOB" runat="server" class="txtMedium" placeholder="dd/mm/yyyy" />
							</p>
							<p>
								<asp:RegularExpressionValidator ID="rvf_re_M3_Forename" runat="server"
									ControlToValidate="M3_Forename" CssClass="Error" Display="Dynamic" ErrorMessage="Forename1 contains invalid characters"
									SetFocusOnError="true" ValidationExpression="^[A-za-z\'\- ]{2,}$" ValidationGroup="valPersonalInfo">Member 3's Forename contains invalid characters</asp:RegularExpressionValidator>
								<asp:RequiredFieldValidator id="rfv_M3_Title" runat="server" ControlToValidate="M3_Title" SetFocusOnError="true"
											CssClass="Error" Display="Dynamic" ValidationGroup="valPersonalInfo"
											ErrorMessage="Sub-member 3 title not selected">Select the title</asp:RequiredFieldValidator>
								<asp:RequiredFieldValidator id="rfv_M3_Forename" runat="server" ControlToValidate="M3_Forename" SetFocusOnError="true"
											CssClass="Error" Display="Dynamic" ValidationGroup="valPersonalInfo"
											ErrorMessage="Sub-member 3 forename not entered">Forename cannot be blank</asp:RequiredFieldValidator>
								<asp:RequiredFieldValidator id="rfv_M3_Surname" runat="server" ControlToValidate="M3_Surname" SetFocusOnError="true"
											CssClass="Error" Display="Dynamic" ValidationGroup="valPersonalInfo"
											ErrorMessage="Sub-member 3 surname not entered">Surname cannot be blank</asp:RequiredFieldValidator>
								<asp:RequiredFieldValidator id="rfv_M3_Gender" runat="server" ControlToValidate="M3_Gender" SetFocusOnError="true"
											CssClass="Error" Display="Dynamic" ValidationGroup="valPersonalInfo"
											ErrorMessage="Sub-member 3 gender not selected">Select the gender</asp:RequiredFieldValidator>
								<asp:RequiredFieldValidator id="rfv_M3_DOB" runat="server" ControlToValidate="M3_DOB" SetFocusOnError="true"
											CssClass="Error" Display="Dynamic" ValidationGroup="valPersonalInfo"
											ErrorMessage="Sub-member 3 DoB not entered">DoB cannot be blank</asp:RequiredFieldValidator>
								<asp:RegularExpressionValidator ID="rvf_re_M3_Surname" runat="server" ControlToValidate="M3_Surname"
											CssClass="Error" Display="Dynamic" ErrorMessage="Surname contains invalid characters"
											SetFocusOnError="true" ValidationExpression="^[A-za-z\'\- ]{2,}$" ValidationGroup="valPersonalInfo">Member 3's Surname contains invalid characters</asp:RegularExpressionValidator>	
								<asp:RegularExpressionValidator ID="rvf_re_M3_DOB" runat="server"
											ControlToValidate="M3_DOB" CssClass="Error" Display="Dynamic" ErrorMessage="DOB3 contains invalid characters"
											SetFocusOnError="true" ValidationExpression="[0123]?\d\/[01]\d\/[12]\d\d\d" ValidationGroup="valPersonalInfo">DOB should be a valid birth date 'dd/mm/yyyy', e.g. 01/01/1969</asp:RegularExpressionValidator>
							</p>
<%
				End If

				If (subMember_count >= 4) Then

%>
							<h3 class="familyMemberRow lineAbove">
								Child Member 4 
								
							</h3>
							<p>
								<label for="M4_Title">Title:</label>
								<select runat="server" id="M4_Title" class="txtMedium">
									<option selected="selected" value="">Title</option>
									<option value="master">Master</option>
									<option value="miss">Miss</option>
									<option value="mr">Mr</option>
									<option value="mrs">Mrs</option>
									<option value="ms">Ms</option>
								</select>
							</p>
							<p>
								<label for="M4_Forename">Forename:</label>
								<input id="M4_Forename" runat="server" class="txtWide" placeholder="Forename" />
							</p>
							<p>
								<label for="M4_Surname">Surname:</label>
								<input id="M4_Surname" runat="server" class="txtWide" placeholder="Surname" />
							</p>
							<p>
								<label>Gender:</label>
								<span class="RadioCheckLabel">
									<asp:RadioButtonList runat="server" id="M4_Gender" RepeatLayout="Flow" EnableTheming="False" RepeatColumns="2" RepeatDirection="Horizontal" ValidationGroup="valPersonalInfo" >
										<asp:ListItem Value="male">Male</asp:ListItem>
										<asp:ListItem Value="female">Female</asp:ListItem>
									</asp:RadioButtonList>
								</span>
	
									
<%
'Save for posterity....  This was the plan for using the 
'							<td><select runat="server" id="M4_SubPackageCode" class="txtMedium" alt="Select the product for this person"></select></td>
%>
							</p>
							<p>
								<label for="M4_DOB">Date of Birth <nobr>(dd/mm/yyyy):</nobr></label>
								<input id="M4_DOB" runat="server" class="txtMedium" placeholder="dd/mm/yyyy" />
							</p>
							<p>
								<asp:RegularExpressionValidator ID="rvf_re_M4_Forename" runat="server"
									ControlToValidate="M4_Forename" CssClass="Error" Display="Dynamic" ErrorMessage="Forename1 contains invalid characters"
									SetFocusOnError="true" ValidationExpression="^[A-za-z\'\- ]{2,}$" ValidationGroup="valPersonalInfo">Member 4's Forename contains invalid characters</asp:RegularExpressionValidator>
									<asp:RequiredFieldValidator id="rfv_M4_Title" runat="server" ControlToValidate="M4_Title" SetFocusOnError="true"
											CssClass="Error" Display="Dynamic" ValidationGroup="valPersonalInfo"
											ErrorMessage="Sub-member 4 title not selected">Select the title</asp:RequiredFieldValidator>
								<asp:RequiredFieldValidator id="rfv_M4_Forename" runat="server" ControlToValidate="M4_Forename" SetFocusOnError="true"
											CssClass="Error" Display="Dynamic" ValidationGroup="valPersonalInfo"
											ErrorMessage="Sub-member 4 forename not entered">Forename cannot be blank</asp:RequiredFieldValidator>
								<asp:RequiredFieldValidator id="rfv_M4_Surname" runat="server" ControlToValidate="M4_Surname" SetFocusOnError="true"
											CssClass="Error" Display="Dynamic" ValidationGroup="valPersonalInfo"
											ErrorMessage="Sub-member 4 surname not entered">Surname cannot be blank</asp:RequiredFieldValidator>
								<asp:RequiredFieldValidator id="rfv_M4_Gender" runat="server" ControlToValidate="M4_Gender" SetFocusOnError="true"
											CssClass="Error" Display="Dynamic" ValidationGroup="valPersonalInfo"
											ErrorMessage="Sub-member 4 gender not selected">Select the gender</asp:RequiredFieldValidator>
								<asp:RequiredFieldValidator id="rfv_M4_DOB" runat="server" ControlToValidate="M4_DOB" SetFocusOnError="true"
											CssClass="Error" Display="Dynamic" ValidationGroup="valPersonalInfo"
											ErrorMessage="Sub-member 4 DoB not entered">DoB cannot be blank</asp:RequiredFieldValidator>
								<asp:RegularExpressionValidator ID="rvf_re_M4_Surname" runat="server" ControlToValidate="M4_Surname"
											CssClass="Error" Display="Dynamic" ErrorMessage="Surname contains invalid characters"
											SetFocusOnError="true" ValidationExpression="^[A-za-z\'\- ]{2,}$" ValidationGroup="valPersonalInfo">Member 4's Surname contains invalid characters</asp:RegularExpressionValidator>	
								<asp:RegularExpressionValidator ID="rvf_re_M4_DOB" runat="server"
											ControlToValidate="M4_DOB" CssClass="Error" Display="Dynamic" ErrorMessage="DOB4 contains invalid characters"
											SetFocusOnError="true" ValidationExpression="[0123]?\d\/[01]\d\/[12]\d\d\d" ValidationGroup="valPersonalInfo">DOB should be a valid birth date 'dd/mm/yyyy', e.g. 01/01/1969</asp:RegularExpressionValidator>
								
							</p>
<%
				End If

				If (subMember_count >= 5) Then

%>
							<h3 class="familyMemberRow lineAbove">
								Child Member 5 
							
							</h3>
							<p>
								<label for="m5_Title">Title:</label>
								<select runat="server" id="m5_Title" class="txtMedium">
									<option selected="selected" value="">Title</option>
									<option value="master">Master</option>
									<option value="miss">Miss</option>
									<option value="mr">Mr</option>
									<option value="mrs">Mrs</option>
									<option value="ms">Ms</option>
								</select>
							</p>
							<p>
								<label for="m5_Forename">Forename:</label>
								<input id="m5_Forename" runat="server" class="txtWide" placeholder="Forename" />
							</p>
							<p>
								<label for="m5_Surname">Surname:</label>
								<input id="m5_Surname" runat="server" class="txtWide" placeholder="Surname" />
							</p>
							<p>
								<label>Gender:</label>
								<span class="RadioCheckLabel">
									<asp:RadioButtonList runat="server" id="m5_Gender" RepeatLayout="Flow" EnableTheming="False" RepeatColumns="2" RepeatDirection="Horizontal" ValidationGroup="valPersonalInfo" >
										<asp:ListItem Value="male">Male</asp:ListItem>
										<asp:ListItem Value="female">Female</asp:ListItem>
									</asp:RadioButtonList>
								</span>
									
<%
'Save for posterity....  This was the plan for using the 
'							<td><select runat="server" id="m5_SubPackageCode" class="txtMedium" alt="Select the product for this person"></select></td>
%>
							</p>
							<p>
								<label for="m5_DOB">Date of Birth <nobr>(dd/mm/yyyy):</nobr></label>
								<input id="m5_DOB" runat="server" class="txtMedium" placeholder="dd/mm/yyyy" />
							</p>
							<p>
								<asp:RegularExpressionValidator ID="rvf_re_m5_Forename" runat="server"
									ControlToValidate="m5_Forename" CssClass="Error" Display="Dynamic" ErrorMessage="Forename1 contains invalid characters"
									SetFocusOnError="true" ValidationExpression="^[A-za-z\'\- ]{2,}$" ValidationGroup="valPersonalInfo">Member 5's Forename contains invalid characters</asp:RegularExpressionValidator>
								<asp:RegularExpressionValidator ID="rvf_re_m5_Surname" runat="server" ControlToValidate="m5_Surname"
											CssClass="Error" Display="Dynamic" ErrorMessage="Surname contains invalid characters"
											SetFocusOnError="true" ValidationExpression="^[A-za-z\'\- ]{2,}$" ValidationGroup="valPersonalInfo">Member 5's Surname contains invalid characters</asp:RegularExpressionValidator>	
								<asp:RequiredFieldValidator id="rfv_M5_Title" runat="server" ControlToValidate="M5_Title" SetFocusOnError="true"
											CssClass="Error" Display="Dynamic" ValidationGroup="valPersonalInfo"
											ErrorMessage="Sub-member 5 Title not selected">Select the title</asp:RequiredFieldValidator>
								<asp:RequiredFieldValidator id="rfv_M5_Forename" runat="server" ControlToValidate="M5_Forename" SetFocusOnError="true"
											CssClass="Error" Display="Dynamic" ValidationGroup="valPersonalInfo"
											ErrorMessage="Sub-member 5 Forename not entered">Forename cannot be blank</asp:RequiredFieldValidator>
								<asp:RequiredFieldValidator id="rfv_M5_Surname" runat="server" ControlToValidate="M5_Surname" SetFocusOnError="true"
											CssClass="Error" Display="Dynamic" ValidationGroup="valPersonalInfo"
											ErrorMessage="Sub-member 5 Surname not entered">Surname cannot be blank</asp:RequiredFieldValidator>
								<asp:RequiredFieldValidator id="rfv_M5_Gender" runat="server" ControlToValidate="M5_Gender" SetFocusOnError="true"
											CssClass="Error" Display="Dynamic" ValidationGroup="valPersonalInfo"
											ErrorMessage="Sub-member 5 Gender not selected">Select the gender</asp:RequiredFieldValidator>
								<asp:RequiredFieldValidator id="rfv_M5_DOB" runat="server" ControlToValidate="M5_DOB" SetFocusOnError="true"
											CssClass="Error" Display="Dynamic" ValidationGroup="valPersonalInfo"
											ErrorMessage="Sub-member 5 DoB not entered">DoB cannot be blank</asp:RequiredFieldValidator>
								<asp:RegularExpressionValidator ID="rvf_re_m5_DOB" runat="server"
											ControlToValidate="m5_DOB" CssClass="Error" Display="Dynamic" ErrorMessage="DOB5 contains invalid characters"
											SetFocusOnError="true" ValidationExpression="[0123]?\d\/[01]\d\/[12]\d\d\d" ValidationGroup="valPersonalInfo">DOB should be a valid birth date 'dd/mm/yyyy', e.g. 01/01/1969</asp:RegularExpressionValidator>
							</p>
							
<%
				End If
							
				If (subMember_count >= 6) Then
%>
							<h3 class="familyMemberRow lineAbove">
								Child Member 6 
							
							</h3>
							<p>
								<label for="m6_Title">Title:</label>
								<select runat="server" id="m6_Title" class="txtMedium">
									<option selected="selected" value="">Title</option>
									<option value="master">Master</option>
									<option value="miss">Miss</option>
									<option value="mr">Mr</option>
									<option value="mrs">Mrs</option>
									<option value="ms">Ms</option>
								</select>
							</p>
							<p>
								<label for="m6_Forename">Forename:</label>
								<input id="m6_Forename" runat="server" class="txtWide" placeholder="Forename" />
							</p>
							<p>
								<label for="m6_Surname">Surname:</label>
								<input id="m6_Surname" runat="server" class="txtWide" placeholder="Surname" />
							</p>
							<p>
								<label>Gender:</label>
								<span class="RadioCheckLabel">
									<asp:RadioButtonList runat="server" id="m6_Gender" RepeatLayout="Flow" EnableTheming="False" RepeatColumns="2" RepeatDirection="Horizontal" ValidationGroup="valPersonalInfo" >
										<asp:ListItem Value="male">Male</asp:ListItem>
										<asp:ListItem Value="female">Female</asp:ListItem>
									</asp:RadioButtonList>
								</span>
									
<%
'Save for posterity....  This was the plan for using the 
'							<td><select runat="server" id="m6_SubPackageCode" class="txtMedium" alt="Select the product for this person"></select></td>
%>
							</p>
							<p>
								<label for="m6_DOB">Date of Birth <nobr>(dd/mm/yyyy):</nobr></label>
								<input id="m6_DOB" runat="server" class="txtMedium" placeholder="dd/mm/yyyy" />
							</p>
							<p>
								<asp:RegularExpressionValidator ID="rvf_re_m6_Forename" runat="server"
									ControlToValidate="m6_Forename" CssClass="Error" Display="Dynamic" ErrorMessage="Forename1 contains invalid characters"
									SetFocusOnError="true" ValidationExpression="^[A-za-z\'\- ]{2,}$" ValidationGroup="valPersonalInfo">Member 5's Forename contains invalid characters</asp:RegularExpressionValidator>
								<asp:RegularExpressionValidator ID="rvf_re_m6_Surname" runat="server" ControlToValidate="m6_Surname"
											CssClass="Error" Display="Dynamic" ErrorMessage="Surname contains invalid characters"
											SetFocusOnError="true" ValidationExpression="^[A-za-z\'\- ]{2,}$" ValidationGroup="valPersonalInfo">Member 5's Surname contains invalid characters</asp:RegularExpressionValidator>	
								<asp:RegularExpressionValidator ID="rvf_re_m6_DOB" runat="server"
											ControlToValidate="m6_DOB" CssClass="Error" Display="Dynamic" ErrorMessage="DOB1 contains invalid characters"
											SetFocusOnError="true" ValidationExpression="[0123]?\d\/[01]\d\/[12]\d\d\d" ValidationGroup="valPersonalInfo">DOB should be a valid birth date 'dd/mm/yyyy', e.g. 01/01/1969</asp:RegularExpressionValidator>
									<asp:RequiredFieldValidator id="rfv_M6_Title" runat="server" ControlToValidate="M6_Title" SetFocusOnError="true"
											CssClass="Error" Display="Dynamic" ValidationGroup="valPersonalInfo"
											ErrorMessage="Sub-member 5 Title not selected">Select the title</asp:RequiredFieldValidator>
								<asp:RequiredFieldValidator id="rfv_M6_Forename" runat="server" ControlToValidate="M6_Forename" SetFocusOnError="true"
											CssClass="Error" Display="Dynamic" ValidationGroup="valPersonalInfo"
											ErrorMessage="Sub-member 5 Forename not entered">Forename cannot be blank</asp:RequiredFieldValidator>
								<asp:RequiredFieldValidator id="rfv_M6_Surname" runat="server" ControlToValidate="M6_Surname" SetFocusOnError="true"
											CssClass="Error" Display="Dynamic" ValidationGroup="valPersonalInfo"
											ErrorMessage="Sub-member 5 Surname not entered">Surname cannot be blank</asp:RequiredFieldValidator>
								<asp:RequiredFieldValidator id="rfv_M6_Gender" runat="server" ControlToValidate="M6_Gender" SetFocusOnError="true"
											CssClass="Error" Display="Dynamic" ValidationGroup="valPersonalInfo"
											ErrorMessage="Sub-member 5 Gender not selected">Select the gender</asp:RequiredFieldValidator>
								<asp:RequiredFieldValidator id="rfv_M6_DOB" runat="server" ControlToValidate="M6_DOB" SetFocusOnError="true"
											CssClass="Error" Display="Dynamic" ValidationGroup="valPersonalInfo"
											ErrorMessage="Sub-member 5 DoB not entered">DoB cannot be blank</asp:RequiredFieldValidator>
							</p>
							
<%
				End If
				
%>				
							
					</div>
				</fieldset>
			</div>
<% End If %>
			

						<div class="FSWrap"><fieldset><legend>Your address details</legend><div>
							<p>Your address details must be those detailed on your bank statement.
							</p>
							<p>
								<label for="PostcodeLookup">UK Postcode:</label>
								<input runat="server" id="PostcodeLookup" class="txtMedium" placeholder="Postcode"
									onkeydown="return checkReturn(event);" />
								<asp:RegularExpressionValidator ID="rvf_re_PostcodeLookup" runat="server" ControlToValidate="PostcodeLookup"
									CssClass="Error" Display="Dynamic" ErrorMessage="Post code is in an invalid format"
									SetFocusOnError="true" ValidationExpression="^[A-Za-z]{1,2}[0-9Rr][0-9A-Za-z]?\s*[0-9][ABDEFGHJLNPQRSTUWXYZabdefghjlnpqrstuwxyz]{2}$"
									ValidationGroup="valPersonalInfo">Contains invalid characters or invalid format</asp:RegularExpressionValidator>
								<asp:RequiredFieldValidator ID="rfv_PostcodeLookup" runat="server" ControlToValidate="PostcodeLookup" CssClass="Error" Display="Dynamic" SetFocusOnError="true" ErrorMessage="Postcode is required" ValidationGroup="valPersonalInfo">Postcode is required</asp:RequiredFieldValidator>
								<a href="#" id="FindAddress" title="Search for your address using the postcode" onclick="AddressLookup();return false;">Find</a>
							</p>
							<div id="AddressResults"></div>
		<script type="text/javascript">
		function checkReturn(e) {
			console.log('keycode='+e.keyCode);
			if (e.keyCode==13) {
				console.log('return key detected; calling AddressLookup()');
				if (e.stopPropagation){
					e.stopPropagation();
				} else if(window.e){
					window.e.cancelBubble=true;
				}
				AddressLookup();
				return false;
			} else {
				return true;
			}
		}
		function stopEvent(e) {
		   if (e.stopPropagation){
			   e.stopPropagation();
		   } else if(window.e){
			  window.e.cancelBubble=true;
		   }
		}
		function AddressLookup() {
			var postcode = $('#PostcodeLookup').val();
			$('#AddressResults').html('<span>Looking up address...</span>').slideDown(0);

			var postdata = { "postcode": postcode, "key": "4e1155f912dac" };

			$.ajax({	url: '/JS/Utils/PostcodeLookup.aspx',
						dataType: 'json',
						data: postdata,
						success: CreateSelect,
						timeout: 5000,
						error: function(jqXHR, status, errorThrown){   //the status returned will be "timeout"
							if (status == 'error') {
								//alert('ERROR');
							} else if (status == 'parsererror') {
								$('#AddressResults').text("We are unable to use the address finder at this moment, please enter it manually below").slideDown(200);
							} else if (status == 'timeout') {
								$('#AddressResults').text("Can't find the address at this moment, please enter it manually below").slideDown(200);
							}
						}
			});
		}
		var addrData;
		function CreateSelect(response) {
			var s = "Please enter your address below:";
		//	console.log('data.results='+response.results);

			try {
				if (!!response.status) {
					if (response.status == 'OK') {
						//	save the data
						addrData = response;
						s = "<select onchange='FillForm(this);'><option>* * * select an address * * *</option>";
						$.each(response.results, function(n){
							s += "<option value='" + n + "'>" + this.s + "</option>";
						});
						s += "</select>";
						$('#PostcodeLookup').val((!!addrData.postcode)?addrData.postcode:$('#PostcodeLookup'));
					} else if (response.status == 'notfound') {
						s = "No addresses found at this postcode:  please check your postcode and try again,  or enter your address below";
					}
				}
			} catch(e) { }
			$('#AddressResults').html(s).slideDown(200);
		}
		function FillForm(sel) {
		//	console.log('select value='+sel.value);
			try {
				if((!!addrData) &&(!!addrData.results)) {
					var a = addrData.results[sel.value];
		//			console.log('select value='+a.toString());
					$('#Address1').val((!!a.a1)?a.a1:'');
					$('#Address2').val((!!a.a2)?a.a2:'');
					$('#Address3').val((!!a.a3)?a.a3:'');
					$('#Address4').val((!!a.a4)?a.a4:'');
					$('#Address5').val((!!a.a5)?a.a5:'');
				}
			} catch(e) { }

		}

		</script>
							<p class="AddressContainer">
								<label for="Address1">Address:</label>
								<em title="Mandatory field">*</em>
								<span class="AddressFieldWrapper">
									<input id="Address1" runat="server" class="txtWide" placeholder="Address" />
									<input id="Address2" runat="server" class="txtWide" />
									<input id="Address3" runat="server" class="txtWide" />
									<input id="Address4" runat="server" class="txtWide" placeholder="Town" />
									<input id="Address5" runat="server" class="txtWide" placeholder="County" />
									<asp:RequiredFieldValidator ID="rvf_Address1" runat="server" ControlToValidate="Address1"
										CssClass="Error" Display="Dynamic" ErrorMessage="Address empty" SetFocusOnError="true"
										ValidationGroup="valPersonalInfo">Your address must be entered</asp:RequiredFieldValidator>
								</span>
							</p>
							<!--
							<p class="FullWidthCheckbox">
								<input type="checkbox" runat="server" id="ExcludeFromMailing" />
								<label for="ExcludeFromMailing">We may send you information about products and services of ours. We may pass
									your details to other selected third party companies for direct marketing purposes. You have a right at
									any time to stop us from contacting you or giving your details to others for these purposes.<br />
									To stop this, tick the box</label>
							</p>
							-->
						</div></fieldset></div>
						
						
											

		<!--

						<div class="FSWrap"></div><fieldset><legend>Personal trainer</legend><div>
							<p>If you currently have a personal trainer, please tell us their name:</p>
							<p>
								<label for="CheckfreeTrainer">Existing trainer name?</label>
								<input type="text" class="txtWide" id="CheckfreeTrainer" runat="server"	/>
							</p>
						</div></fieldset>
		-->
		<% If Me.SBP.Branch.MarketingSource IsNot Nothing Then %>
						<div class="FSWrap"><fieldset><legend>Marketing Info</legend><div>
							<p>
								<label for="MarketingSource">How did you hear about us?:</label>
								<em title="Mandatory field">*</em>
								<asp:DropDownList runat="server" id="MarketingSource" CssClass="txtWide" />
								<asp:RequiredFieldValidator ID="rvf_MarketingSource" runat="server" ControlToValidate="MarketingSource"
									CssClass="Error" Display="Dynamic" ErrorMessage="How you heard about us needs to be selected"
									SetFocusOnError="true" ValidationGroup="valPersonalInfo">Please let us know how you heard about us needs to be selected</asp:RequiredFieldValidator>
							</p>
						</div></fieldset></div>
		<% End If %>

						<p class="Buttons">
							<asp:Button id="btnPersonalInfo_Next" runat="server"  CssClass="btnNext" Text="Next step" ValidationGroup="valPersonalInfo" UseSubmitBehavior="false" />
							<asp:Button ID="btnPersonalInfo_Prev" runat="server" CssClass="btnSecondary" Text="Previous step" CausesValidation="false" ValidationGroup="valPersonalInfo" />
						</p>
					</asp:View>

					<asp:View id="viewAgreement" runat="server">

						<asp:ValidationSummary runat="server" id="valSum_Agreement" ValidationGroup="valAgreement" CssClass="ErrorSummary"
							HeaderText="You must agree to the conditions in order to proceed"  ShowMessageBox="False" />

						<div class="FSWrap"><fieldset><legend>Terms of membership</legend><div>
							<p>
								The <a href="<%= Me.SBP.Product.TermsAndConditionsDocument %>" target="_blank">terms and conditions are available for you to Download Here</a>.
								 It is important you understand these before continuing as they contain important information regarding your Statutory Rights including rights of cancellation which you should ensure you read and understand before you continue.</p>
						</div></fieldset></div>

						<div class="FSWrap"><fieldset><legend>IMPORTANT - USE OF YOUR INFORMATION</legend><div>
							<p>
								You have a right to know how we will use your personal information. The <a href="<%=PrivacyPolicyDocument %>" target="_blank">privacy policy
								is available for you to download</a> (PDF). It is important you understand these before continuing.
							</p>
						</div></fieldset></div>
						
						
						<div class="FSWrap"><fieldset><legend>Your contact preferences</legend><div>
								<p>From time to time, we may want to send you information about the service(s) we offer.<br />
								I give you permission to contact me by:</p>
								<p class="CheckOptsList">
									<asp:CheckBoxList runat="server" ID="MarketingCommsPreferences" RepeatDirection="Vertical" RepeatLayout="Flow">
										<asp:ListItem Value="email">Email</asp:ListItem>
										<asp:ListItem Value="sms">SMS / Messaging</asp:ListItem>
										<asp:ListItem Value="phone">Telephone</asp:ListItem>
										<asp:ListItem Value="post">Post</asp:ListItem>
									</asp:CheckBoxList>
								</p>

								<p>We may pass your details to other selected third party companies for direct marketing purposes.<br />
							<!--	Please select the methods of communication you would prefer.	-->
								I give you permission to contact me by:</p>
								<p class="CheckOptsList">
									<asp:CheckBoxList runat="server" ID="ThirdPartyCommsPreferences" RepeatDirection="Vertical" RepeatLayout="Flow">
										<asp:ListItem Value="email">Email</asp:ListItem>
										<asp:ListItem Value="sms">SMS / Messaging</asp:ListItem>
										<asp:ListItem Value="phone">Telephone</asp:ListItem>
										<asp:ListItem Value="post">Post</asp:ListItem>
									</asp:CheckBoxList>
								</p>
						</div></fieldset></div> 


						<div class="FSWrap"><fieldset><legend>Declaration</legend><div>
							<p>
								You warrant, declare and acknowledge that:
							</p>
							<p>
								1. The information given by you in entering this agreement is correct and will be relied upon by us.
							</p>
							<p>
								2. To the best of your knowledge and belief you are in good health and not knowingly incapable of engaging
								in either active or passive exercise and that such exercise would not be detrimental to your health, safety,
								comfort, well being or physical condition. Further, that you will advise us immediately should your
								circumstances change.
							</p>
							<p>
								3. You have read this agreement, including the terms and conditions before accepting them below.
							</p>
							<p>
								4. This agreement will become binding upon both parties once you have ticked in the "I agree" box below
								and the "Next step" button has been clicked.
							</p>
							<p>
								5. <asp:Literal runat="server" ID="litAgreementAgreeingTo" />
							</p>
							<p>
								Please ensure you have sufficient funds available in your bank account, as your bank may charge you a fee for a failed Direct Debit payment
							</p>
							<p class="Text">
								6. You request that we begin the service immediately after you have signed up.
							</p>
		<script type="text/javascript">
		function AgreeToTandC(source, args){
			if ($("#AgreeToConditions").is(":checked")) {
				args.IsValid = true;
			} else {
				$('html, body').animate({scrollTop: $('.LHCheckbox').offset().top-20},1000);
				args.IsValid = false;
			}
		}
		</script>
							<p class="LHCheckbox">
								<label for="AgreeToConditions">I agree</label>
								<input runat="server" id="AgreeToConditions" type="checkbox" />
								<asp:CustomValidator ID="vAgreeToConditions" runat="server" ValidationGroup="valAgreement"
									cssclass="WideError" Display="Dynamic"
									ClientValidationFunction="AgreeToTandC" OnServerValidate="AgreedToTerms"
									>You must agree to these terms and conditions in order to proceed with signing up</asp:CustomValidator>
							</p>
						</div></fieldset></div>

						<p class="Buttons">
							<asp:Button id="btnAgreement_Next" runat="server" CssClass="btnNext" ValidationGroup="valAgreement" Text="Next step" CausesValidation="true" />
							<asp:Button id="btnAgreement_Prev" runat="server" CssClass="btnSecondary" ValidationGroup="valAgreement" Text="Previous step" CausesValidation="false" />
						</p>
					</asp:View>

					<asp:View id="viewBankDetails" runat="server">

		<script type="text/javascript">
		$(function(){
			//  Scroll to any card errors or the DDMandate
			if ( $('.Error').is(':visible') ) {
				$('html, body').animate({scrollTop: $('#CCForm').offset().top-20},1000);
			} else if ( $('#DDMandatePreview').is(':visible') ) {
				$('html, body').animate({scrollTop: $('#DDMandatePreview').offset().top-20},1000);
			}
		});
		</script>

						
		<script type="text/javascript">
						// for browsers that don't have consoles... (i.e. IE)
			if (typeof console == "undefined" || typeof console.debug == "undefined") var console = { debug: function () { } };
		</script>

		<%-- Voyager --%>
		<asp:Literal ID="litHTMLHeadScript" runat="server" />
		<div id="CCForm" runat="server">
							<div class="FSWrap">
								<asp:HiddenField ID="payByCC" runat="server" EnableViewState="true" />
								<fieldset class="First">
									<legend>Your card details</legend>
									<div style="padding-bottom:20px;">
										<asp:Literal ID="litCCErrors" runat="server" EnableViewState="false" Visible="false" />
										<p>
											<label for="CC_CardName" title="The cardholder's name exactly as it appears on the card">
												Name on card:</label>
											<em title="Mandatory field">*</em>
											<input id="CC_CardName" runat="server" autocomplete="off" placeholder="Card name"
												class="txtWide" maxlength="50" onfocus="ShowHelp('helpCC_name');" />
											<asp:RequiredFieldValidator ID="rfv_CC_CardName" runat="server" ControlToValidate="CC_CardName"
												CssClass="Error" Display="Dynamic" ErrorMessage="Cardholder's name not entered"
												SetFocusOnError="true" ValidationGroup="valBankDetails">Enter the cardholder's name</asp:RequiredFieldValidator>
											<span id="helpCC_name" class="FieldHelpText">Enter the cardholder's name as it appears
												on the card</span>
										</p>
										<p>
											<label for="CC_CardType">Card type:</label>
											<em title="Mandatory field">*</em>
											<select id="CC_CardType" runat="server" class="txtWide" onfocus="ShowHelp('helpCC_cardType');">
												<option value="">-- Choose</option>
												<option value="MC">Mastercard</option>
												<option value="VISA">Visa</option>
												<option value="DELTA">Visa Delta/Debit</option>
												<option value="UKE">Visa Electron</option>
											</select>
											<asp:RequiredFieldValidator ID="rfv_CC_CardType" runat="server" ControlToValidate="CC_CardType"
												CssClass="Error" Display="Dynamic" ErrorMessage="Card type not selected" SetFocusOnError="true"
												ValidationGroup="valBankDetails">Enter the card type</asp:RequiredFieldValidator>
											<span id="helpCC_cardType" class="FieldHelpText">Select the type of card you have</span>
										</p>
										<p>
											<label for="CC_CardNumber">Card number<br />(no spaces):</label>
											<em title="Mandatory field">*</em>
											<input id="CC_CardNumber" runat="server" autocomplete="off" placeholder="Card number"
												class="txtWide" maxlength="20" onfocus="ShowHelp('helpCC_cardNo');" />
											<asp:RequiredFieldValidator ID="rfv_CC_CardNumber" runat="server" ControlToValidate="CC_CardNumber"
												CssClass="Error" Display="Dynamic" ErrorMessage="Enter the card number" SetFocusOnError="true"
												ValidationGroup="valBankDetails">Enter the card number</asp:RequiredFieldValidator>
											<asp:RegularExpressionValidator ID="vx_CC_CardNumberRegex" runat="server" ControlToValidate="CC_CardNumber"
												CssClass="Error" Display="Dynamic" ErrorMessage="Enter the long card number with no spaces"
												SetFocusOnError="true" ValidationExpression="^\d{12,19}$" ValidationGroup="valBankDetails">Invalid card number</asp:RegularExpressionValidator>
											<span id="helpCC_cardNo" class="FieldHelpText">Enter the long card number without spaces
												or other characters</span>
										</p>
										<p>
											<label for="CC_CVV">Security code:</label>
											<em title="Mandatory field">*</em>
											<input id="CC_CVV" runat="server" autocomplete="off" placeholder="CVN"
												class="txtMedium" maxlength="4" onfocus="ShowHelp('helpCC_CVN');" />
											<asp:RequiredFieldValidator ID="rfv_CC_CVV" runat="server" ControlToValidate="CC_CVV"
												CssClass="Error" Display="Dynamic" ErrorMessage="Enter the card verification number"
												SetFocusOnError="true" ValidationGroup="valBankDetails">Enter the card verification number (normally the last three digits in the signature strip on the back of the card)</asp:RequiredFieldValidator>
											<asp:RegularExpressionValidator ID="rfv_CC_CVVRegex" runat="server" ControlToValidate="CC_CVV"
												CssClass="Error" Display="Dynamic" ErrorMessage="Enter the card verification number"
												SetFocusOnError="true" ValidationExpression="^\d{3,4}$" ValidationGroup="valBankDetails">Enter the card verification number (the last three digits in the signature strip on the back of the card)</asp:RegularExpressionValidator>
											<span id="helpCC_CVN" class="FieldHelpText">Enter the three digit number at the end
												of the signature strip on the back of the card</span>
										</p>
										<p>
											<label>Expiry date:</label>
											<em title="Mandatory field">*</em>
											<label for="CC_Expiry_M" style="display: none;">Card expiry month</label>
											<select id="CC_Expiry_M" runat="server" class="txtNarrow" onfocus="ShowHelp('helpCC_expiryDate');">
												<option value="">Month</option>
												<option value="01">1</option>
												<option value="02">2</option>
												<option value="03">3</option>
												<option value="04">4</option>
												<option value="05">5</option>
												<option value="06">6</option>
												<option value="07">7</option>
												<option value="08">8</option>
												<option value="09">9</option>
												<option value="10">10</option>
												<option value="11">11</option>
												<option value="12">12</option>
											</select>
											<label for="CC_Expiry_Y" style="display: none;">Card expiry year</label>
											<select id="CC_Expiry_Y" runat="server" class="txtMedium noClear" onfocus="ShowHelp('helpCC_expiryDate');">
												<option value="">Year</option>
												<option value="18">2018</option>
												<option value="19">2019</option>
												<option value="20">2020</option>
												<option value="21">2021</option>
												<option value="22">2022</option>
												<option value="23">2023</option>
												<option value="24">2024</option>
												<option value="25">2025</option>
												<option value="26">2026</option>
												<option value="27">2027</option>
												<option value="28">2028</option>
												<option value="29">2029</option>
												<option value="30">2030</option>
												<option value="31">2031</option>
											</select>
											<asp:RequiredFieldValidator ID="rfv_CC_Expiry_M" runat="server" ControlToValidate="CC_Expiry_M"
												CssClass="Error" Display="Dynamic" ErrorMessage="Card expiry month not selected"
												SetFocusOnError="true" ValidationGroup="valBankDetails">Enter the card expiry month</asp:RequiredFieldValidator>
											<asp:RequiredFieldValidator ID="rfv_CC_Expiry_Y" runat="server" ControlToValidate="CC_Expiry_Y"
												CssClass="Error" Display="Dynamic" ErrorMessage="Card expiry year not specified"
												SetFocusOnError="true" ValidationGroup="valBankDetails">Enter the card expiry year</asp:RequiredFieldValidator>
											<span id="helpCC_expiryDate" class="FieldHelpText">Enter the card's expiry date</span>
										</p>
										<p class="HeadingText" style="padding-top:1em;">Card billing address including postcode (if applicable)</p>
										<p>Please use the card billing address if different from your home address.</p>
										<p>
											<label for="CC_Address1">Address line 1:</label>
											<em title="Mandatory field">*</em>
											<input id="CC_Address1" runat="server" class="txtWide" placeholder="Address" onfocus="ShowHelp('helpCC_address');"/>
											<asp:RequiredFieldValidator ID="rvf_CCAddress1" runat="server" ControlToValidate="CC_Address1"
												CssClass="Error" Display="Dynamic" ErrorMessage="Address empty" SetFocusOnError="true"
												ValidationGroup="valBankDetails">Your card billing address must be entered</asp:RequiredFieldValidator>
											<span id="helpCC_address" class="FieldHelpText">Enter your street address as it appears
												on your card statement</span>
										</p>
										<p>
											<label for="CC_Address2">Address line 2:</label>
											<input id="CC_Address2" runat="server" class="txtWide" onfocus="ShowHelp('helpCC_address');" />
										</p>
										<p>
											<label for="CC_City">City / town:</label>
											<em title="Mandatory field">*</em>
											<input id="CC_City" runat="server" class="txtWide" onfocus="ShowHelp('helpCC_city');" />
											<asp:RequiredFieldValidator ID="rfv_CCCity" runat="server" ControlToValidate="CC_City"
												CssClass="Error" Display="Dynamic" ErrorMessage="City empty" SetFocusOnError="true"
												ValidationGroup="valBankDetails">Your card billing city/town must be entered</asp:RequiredFieldValidator>
											<span id="helpCC_city" class="FieldHelpText">Enter the city/town of your address</span>
										</p>
										<p>
											<label for="CC_County">County:</label>
											<input id="CC_County" runat="server" class="txtWide" onfocus="ShowHelp('helpCC_county');" />
											<span id="helpCC_county" class="FieldHelpText">Enter your county or region</span>
										</p>
										<p>
											<label for="CC_Postcode">Postcode:</label>
											<input id="CC_Postcode" runat="server" class="txtWide" onfocus="ShowHelp('helpCC_postcode');" />
											<span id="helpCC_postcode" class="FieldHelpText">Enter your postcode - mandatory for
												UK addresses</span>
										</p>
										<p>
											<label for="CC_Country">Country:</label>
											<em title="Mandatory field">*</em>
											<select id="CC_Country" runat="server" class="txtWide" onfocus="ShowHelp('helpCC_country');"
												size="1">
												<option value="GB">United Kingdom</option>
												<option value="AF">Afghanistan</option><option value="AL">Albania</option><option value="DZ">Algeria</option><option value="AS">American Samoa</option><option value="AD">Andorra</option><option value="AO">Angola</option><option value="AI">Anguilla</option><option value="AQ">Antarctica</option><option value="AG">Antigua and Barbuda</option><option value="AR">Argentina</option><option value="AM">Armenia</option><option value="AW">Aruba</option><option value="AU">Australia</option><option value="AT">Austria</option><option value="AZ">Azerbaidjan</option><option value="BS">Bahamas</option><option value="BH">Bahrain</option><option value="BD">Bangladesh</option><option value="BB">Barbados</option><option value="BY">Belarus</option><option value="BE">Belgium</option><option value="BZ">Belize</option><option value="BJ">Benin</option><option value="BM">Bermuda</option><option value="BT">Bhutan</option><option value="BO">Bolivia</option><option value="BA">Bosnia-Herzegovina</option><option value="BW">Botswana</option><option value="BV">Bouvet Island</option><option value="BR">Brazil</option><option value="IO">British Indian Ocean Territory</option><option value="BN">Brunei Darussalam</option><option value="BG">Bulgaria</option><option value="BF">Burkina Faso</option><option value="BI">Burundi</option><option value="KH">Cambodia</option><option value="CM">Cameroon</option><option value="CA">Canada</option><option value="CV">Cape Verde</option><option value="KY">Cayman Islands</option><option value="CF">Central African Republic</option><option value="TD">Chad</option><option value="CL">Chile</option><option value="CN">China</option><option value="CX">Christmas Island</option><option value="CC">Cocos (Keeling) Islands</option><option value="CO">Colombia</option><option value="KM">Comoros</option><option value="CG">Congo</option><option value="CK">Cook Islands</option><option value="CR">Costa Rica</option><option value="HR">Croatia</option><option value="CU">Cuba</option><option value="CY">Cyprus</option><option value="CZ">Czech Republic</option><option value="DK">Denmark</option><option value="DJ">Djibouti</option><option value="DM">Dominica</option><option value="DO">Dominican Republic</option><option value="TP">East Timor</option><option value="EC">Ecuador</option><option value="EG">Egypt</option><option value="SV">El Salvador</option><option value="GQ">Equatorial Guinea</option><option value="ER">Eritrea</option><option value="EE">Estonia</option><option value="ET">Ethiopia</option><option value="FK">Falkland Islands</option><option value="FO">Faroe Islands</option><option value="FJ">Fiji</option><option value="FI">Finland</option><option value="CS">Former Czechoslovakia</option><option value="SU">Former USSR</option><option value="FR">France</option><option value="FX">France (European Territory)</option><option value="GF">French Guyana</option><option value="TF">French Southern Territories</option><option value="GA">Gabon</option><option value="GM">Gambia</option><option value="GE">Georgia</option><option value="DE">Germany</option><option value="GH">Ghana</option><option value="GI">Gibraltar</option><option value="GB">Great Britain</option><option value="GR">Greece</option><option value="GL">Greenland</option><option value="GD">Grenada</option><option value="GP">Guadeloupe (French)</option><option value="GU">Guam (USA)</option><option value="GT">Guatemala</option><option value="GN">Guinea</option><option value="GW">Guinea Bissau</option><option value="GY">Guyana</option><option value="HT">Haiti</option><option value="HM">Heard and McDonald Islands</option><option value="HN">Honduras</option><option value="HK">Hong Kong</option><option value="HU">Hungary</option><option value="IS">Iceland</option><option value="IN">India</option><option value="ID">Indonesia</option><option value="INT">International</option><option value="IR">Iran</option><option value="IQ">Iraq</option><option value="IE">Ireland</option><option value="IL">Israel</option><option value="IT">Italy</option><option value="CI">Ivory Coast (Cote D&#39;Ivoire)</option><option value="JM">Jamaica</option><option value="JP">Japan</option><option value="JO">Jordan</option><option value="KZ">Kazakhstan</option><option value="KE">Kenya</option><option value="KI">Kiribati</option><option value="KW">Kuwait</option><option value="KG">Kyrgyzstan</option><option value="LA">Laos</option><option value="LV">Latvia</option><option value="LB">Lebanon</option><option value="LS">Lesotho</option><option value="LR">Liberia</option><option value="LY">Libya</option><option value="LI">Liechtenstein</option><option value="LT">Lithuania</option><option value="LU">Luxembourg</option><option value="MO">Macau</option><option value="MK">Macedonia</option><option value="MG">Madagascar</option><option value="MW">Malawi</option><option value="MY">Malaysia</option><option value="MV">Maldives</option><option value="ML">Mali</option><option value="MT">Malta</option><option value="MH">Marshall Islands</option><option value="MQ">Martinique (French)</option><option value="MR">Mauritania</option><option value="MU">Mauritius</option><option value="YT">Mayotte</option><option value="MX">Mexico</option><option value="FM">Micronesia</option><option value="MD">Moldavia</option><option value="MC">Monaco</option><option value="MN">Mongolia</option><option value="MS">Montserrat</option><option value="MA">Morocco</option><option value="MZ">Mozambique</option><option value="MM">Myanmar</option><option value="NA">Namibia</option><option value="NR">Nauru</option><option value="NP">Nepal</option><option value="NL">Netherlands</option><option value="AN">Netherlands Antilles</option><option value="NT">Neutral Zone</option><option value="NC">New Caledonia (French)</option><option value="NZ">New Zealand</option><option value="NI">Nicaragua</option><option value="NE">Niger</option><option value="NG">Nigeria</option><option value="NU">Niue</option><option value="NF">Norfolk Island</option><option value="KP">North Korea</option><option value="MP">Northern Mariana Islands</option><option value="NO">Norway</option><option value="OM">Oman</option><option value="PK">Pakistan</option><option value="PW">Palau</option><option value="PA">Panama</option><option value="PG">Papua New Guinea</option><option value="PY">Paraguay</option><option value="PE">Peru</option><option value="PH">Philippines</option><option value="PN">Pitcairn Island</option><option value="PL">Poland</option><option value="PF">Polynesia (French)</option><option value="PT">Portugal</option><option value="PR">Puerto Rico</option><option value="QA">Qatar</option><option value="RE">Reunion (French)</option><option value="RO">Romania</option><option value="RU">Russian Federation</option><option value="RW">Rwanda</option><option value="GS">S. Georgia & S. Sandwich Isls.</option><option value="SH">Saint Helena</option><option value="KN">Saint Kitts & Nevis Anguilla</option><option value="LC">Saint Lucia</option><option value="PM">Saint Pierre and Miquelon</option><option value="ST">Saint Tome (Sao Tome) and Principe</option><option value="VC">Saint Vincent & Grenadines</option><option value="WS">Samoa</option><option value="SM">San Marino</option><option value="SA">Saudi Arabia</option><option value="SN">Senegal</option><option value="SC">Seychelles</option><option value="SL">Sierra Leone</option><option value="SG">Singapore</option><option value="SK">Slovak Republic</option><option value="SI">Slovenia</option><option value="SB">Solomon Islands</option><option value="SO">Somalia</option><option value="ZA">South Africa</option><option value="KR">South Korea</option><option value="ES">Spain</option><option value="LK">Sri Lanka</option><option value="SD">Sudan</option><option value="SR">Suriname</option><option value="SJ">Svalbard and Jan Mayen Islands</option><option value="SZ">Swaziland</option><option value="SE">Sweden</option><option value="CH">Switzerland</option><option value="SY">Syria</option><option value="TJ">Tadjikistan</option><option value="TW">Taiwan</option><option value="TZ">Tanzania</option><option value="TH">Thailand</option><option value="TG">Togo</option><option value="TK">Tokelau</option><option value="TO">Tonga</option><option value="TT">Trinidad and Tobago</option><option value="TN">Tunisia</option><option value="TR">Turkey</option><option value="TM">Turkmenistan</option><option value="TC">Turks and Caicos Islands</option><option value="TV">Tuvalu</option><option value="UG">Uganda</option><option value="UA">Ukraine</option><option value="AE">United Arab Emirates</option><option value="UY">Uruguay</option><option value="US">United States</option><option value="UZ">Uzbekistan</option><option value="VU">Vanuatu</option><option value="VA">Vatican City State</option><option value="VE">Venezuela</option><option value="VN">Vietnam</option><option value="VG">Virgin Islands (British)</option><option value="VI">Virgin Islands (USA)</option><option value="WF">Wallis and Futuna Islands</option><option value="EH">Western Sahara</option><option value="YE">Yemen</option><option value="YU">Yugoslavia</option><option value="ZR">Zaire</option><option value="ZM">Zambia</option><option value="ZW">Zimbabwe</option>
											</select>
											<asp:RequiredFieldValidator ID="rfv_CC_Country" runat="server" ControlToValidate="CC_Country"
												CssClass="Error" Display="Dynamic" ErrorMessage="Select the country" SetFocusOnError="true"
												ValidationGroup="valBankDetails">Select the country</asp:RequiredFieldValidator>
										</p>
									</div>
								</fieldset>
							</div>
						</div><!-- /CCForm	-->

						<div id="DDForm" runat="server">
							<div class="FSWrap">
								<fieldset class="First">
									<legend>Your bank details</legend>
									<div>

										<asp:Literal runat="server" ID="litvalBankDetailsPaymentDetails" />

										<p>
											<img class="DDLogo" src="/Clients/_MasterDocs/images/DDLogo_99x31_transparent.png" alt="Direct Debit logo" />
											The company name which will appear on your bank statement against the Direct Debit will be: <%=Globals.OriginatorName%>
										</p>

										<div id="DDForm_ViewMandate_incomplete">
											<p>Enter your <strong>bank account details for the Direct Debit</strong>. We will email
												you a copy of your Direct Debit Instruction.</p>
										</div>
										<div id="DDForm_ViewMandate_complete" style="display:none;">
											<p>
												We will email you a copy of your Direct Debit Instruction. If you want to see a copy now,
												<asp:LinkButton ID="btnDDMandatePreview" runat="server" CausesValidation="true" CssClass="SecondaryLink"
													OnClientClick="var x=getElementById('Signup');if(x.action.indexOf('DDMandatePreview')<0) { x.action+='#DDMandatePreview';}">click
													here to display a copy of your Direct Debit Instruction</asp:LinkButton>
											</p>
										</div>

										<asp:ValidationSummary runat="server" id="valSum_BankDetails" ValidationGroup="valBankDetails"
											HeaderText="Please correct the following errors:" CssClass="ErrorSummary" ShowMessageBox="True" />

										<asp:Literal runat="server" ID="litBankDetailsErrors" Visible="false" />

										<p>
											<label for="AccountName">Account holder name:</label>
											<em title="Mandatory field">*</em>
											<input id="AccountName" runat="server" autocomplete="off" placeholder="Account name"
												class="txtWide" onfocus="ShowHelp('helpAcctName');" />
											<asp:RequiredFieldValidator id="rfv_DD_AcctName" runat="server" ControlToValidate="AccountName" SetFocusOnError="true"
												CssClass="Error" Display="Dynamic" ValidationGroup="valBankDetails"
												ErrorMessage="Account name not entered">Account name cannot be blank</asp:RequiredFieldValidator>
											<span id="helpAcctName" class="FieldHelpText">Please provide the name on your account,
												i.e. the name on your bank card. Please note we cannot accept Direct Debit payments
												from outside the UK.</span>
										</p>
										<p>
											<label for="SortCode">Sort code (6 digits):</label>
											<em title="Mandatory field">*</em>
											<input id="SortCode" runat="server" autocomplete="off" placeholder="Sort code"
												class="txtWide" onfocus="ShowHelp('helpSortCode');" />
											<asp:RequiredFieldValidator id="rfv_DD_SortCode" runat="server" ControlToValidate="SortCode" SetFocusOnError="true"
												CssClass="Error" Display="Dynamic" ValidationGroup="valBankDetails"
												ErrorMessage="Sort code not entered">Sort code cannot be blank</asp:RequiredFieldValidator>
											<asp:RegularExpressionValidator id="rfv_DD_SortCodeRegex" runat="server"  SetFocusOnError="true" ControlToValidate="SortCode"
												ErrorMessage="Invalid bank sort code"
												 CssClass="Error" Display="Dynamic" ValidationGroup="valBankDetails"
												ValidationExpression="^\d\d[\-\s]??\d\d[\-\s]??\d\d$">Invalid bank sort code (6 digits only - no spaces or other characters)</asp:RegularExpressionValidator>
											<span id="helpSortCode" class="FieldHelpText">Please provide your sort code, i.e. 6
												digits</span>
										</p>
										<p>
											<label for="AccountNumber">Account number<br />(7 or 8 digits):</label>
											<em title="Mandatory field">*</em>
											<input id="AccountNumber" runat="server" autocomplete="off" placeholder="Act. number"
												class="txtWide" onfocus="ShowHelp('helpAcctNum');" />
											<asp:RequiredFieldValidator id="rfv_DD_AcctNo" runat="server" ControlToValidate="AccountNumber" SetFocusOnError="true"
												CssClass="Error" Display="Dynamic" ValidationGroup="valBankDetails"
												ErrorMessage="Account name not entered">Account name cannot be blank</asp:RequiredFieldValidator>
											<asp:RegularExpressionValidator id="rfv_DD_AcctNoRegex" runat="server" ControlToValidate="AccountNumber"
												CssClass="Error" Display="Dynamic" ErrorMessage="Invalid bank account number"
												SetFocusOnError="true" ValidationExpression="^\d{7,8}$" ValidationGroup="valBankDetails">Invalid bank account number (7 or 8 digits only -
												no spaces or other characters)</asp:RegularExpressionValidator>
											<span id="helpAcctNum" class="FieldHelpText">Please provide your account number, i.e.
												7 or 8 digits</span>
										</p>

		<script type="text/javascript">
		//	Static globals
		var reName = /\w{3,}/;
		var reSort = /[\d-\s]{6,}/;
		var reAcno = /\d{7,8}/;

		//	Method to validate bank details prior to full server validation
		function EnableDDMandatePreview() {
			var acctName = document.getElementById('AccountName').value;
			var acctAcno = document.getElementById('AccountNumber').value;
			var acctSort = document.getElementById('SortCode').value;
			var isValid = false;

			if ((acctName.length > 0) && (reName.test(acctName))) {
				if ((acctAcno.length > 0) && (reAcno.test(acctAcno))) {
					if ((acctSort.length > 0) && (reSort.test(acctSort))) {
						isValid = true;
					}
				}
			}
			if (isValid) {
					document.getElementById('DDForm_ViewMandate_incomplete').style.display = "none";
					document.getElementById('DDForm_ViewMandate_complete').style.display = "block";
			} else {
					document.getElementById('DDForm_ViewMandate_incomplete').style.display = "block";
					document.getElementById('DDForm_ViewMandate_complete').style.display = "none";
			}
		}
		document.onkeyup = EnableDDMandatePreview;	//	Add keyUp event
		EnableDDMandatePreview();					//  call on page load
		</script>

									</div>
								</fieldset>
							</div>

						</div><!-- /DDForm -->

						
					<div id="SEPAForm" runat="server">
						<fieldset class="First">
						<legend><%=GetTranslation("BankDetails_Legend")%></legend>
						<div>
							<p><%=String.Format(GetTranslation("BankDetailsSEPA_CreditorID"), Globals.OriginatorNumber)%></p>
							<p><%=String.Format(GetTranslation("BankDetailsSEPA_IntroText"), Globals.OriginatorName)%></p>
							<p><%=GetTranslation("BankDetailsSEPA_IntroText2")%></p>
							<p style="padding-top:1em;font-weight:bold;"><%=GetTranslation("BankDetailsSEPA_EnterDetails")%></p>
							<asp:ValidationSummary runat="server" id="valSum_SEPABankDetails" ValidationGroup="valBankDetails"
								HeaderText='<%# GetTranslation("SummaryBankDetails_HeaderText")%>' CssClass="ErrorSummary" ShowMessageBox="True" />
							<asp:Literal runat="server" ID="litSEPABankDetailsErrors" Visible="false" />
							<SnapControl:SnapSEPAControl
								ID="SnapSEPAControl"
								runat="server"
								enableviewstate="true" />
							<p></p>
							<p><%=String.Format(GetTranslation("BankDetailsSEPA_OutroText"), Globals.OriginatorName)%></p>
<% If (SBP.View_showPaymentType_HybridCardSEPADD) And _
   (SBP.View_CurrentPaymentType = SnapDDA.GlobalSettings.PaymentType.SEPADD) Then %>
							<p>
								<%=GetTranslation("PaymentType_HybridCardSEPADD")%>
								<asp:LinkButton ID="PayBy_HybridCardSEPADD" runat="server" CssClass="PaymentTypeChoice"
									ToolTip='<%# GetTranslation("PaymentType_HybridCardSEPADD_tooltop")%>'><%=GetTranslation("PaymentType_buttontext")%></asp:LinkButton>
							</p>
<% End If %>
<% If (SBP.View_showPaymentType_RecurringCard) And _
   ((SBP.View_CurrentPaymentType = SnapDDA.GlobalSettings.PaymentType.SEPADD) Or (SBP.View_CurrentPaymentType = SnapDDA.GlobalSettings.PaymentType.HybridCardSEPADD)) Then %>
							<p>
								<%=GetTranslation("PaymentType_RecurringCard")%>
								<asp:LinkButton ID="PayBy_RecurringCard3" runat="server" CssClass="PaymentTypeChoice"
									ToolTip='<%# GetTranslation("PaymentType_RecurringCard_tooltop")%>'><%=GetTranslation("PaymentType_buttontext")%></asp:LinkButton>
							</p>
<% End If %>
						</div>
						</fieldset>
					</div><!-- /SEPAForm -->
<div id="DDMandatePreview" runat="server" visible="false">
							<div class="FSWrap">
								<fieldset>
									<legend>Direct Debit Instruction</legend>
									<div>
										<p>Please print off and retain for your records.</p>
										<table>
											<tr>
												<td class="HalfL">
													<p>Bank Service User name and address:</p>
													<p><%=Globals.OriginatorNameAndAddress%></p>
													<p>Name(s) of Account Holder(s):<br />
														<b><asp:Literal ID="litDDAccountHolderName" runat="server" /></b></p>
													<p>Bank / Building Society Account Number:<br />
														<b><asp:Literal ID="litDDAccountNumber" runat="server" /></b></p>
													<p>Branch Sort Code:<br />
														<b><asp:Literal ID="litDDSortCode" runat="server" /></b></p>
													<p>Name and Address of your Bank or Building Society:<br />
														<b><asp:Literal ID="litDDBankAddress" runat="server" /></b></p>
												</td>
												<td class="HalfR">
													<p>Instruction to your Bank or Building Society to pay by Direct Debit</p>
													<p>Service User Number: <b><%=Globals.OriginatorNumber%></b></p>
													<p>Reference: <b><asp:Literal ID="litDDTransRefNo" runat="server" /></b></p>
													<p>Instruction to your Bank or Building Society:<br />
														Please pay
														<%=Globals.OriginatorName%>
														Direct Debits from the account detailed in this Instruction subject to the safeguards
														assured by the Direct Debit Guarantee. I understand that this Instruction may remain with
														<%=Globals.OriginatorName%>
														and, if so, details will be passed electronically to my Bank.</p>
													<p>Date: <b><asp:Literal ID="litDDDate" runat="server"></asp:Literal></b></p>
												</td>
											</tr>
										</table>
									</div>
								</fieldset>
							</div>
							<div class="FSWrap">
								<fieldset>
									<legend>Direct Debit Guarantee</legend>
									<div>
										<img alt="Direct Debit logo" src="/Clients/_MasterDocs/images/DDLogo_99x31_transparent.png" class="DDLogoAbPos" />
										<p>Please print off and retain for your records</p>
										<p>The Direct Debit Guarantee</p>
										<ul class="DDGuar">
											<li>
												This Guarantee is offered by all Banks and Building Societies that accept instructions to pay Direct Debits.
											</li>
											<li>If there are any changes to the amount, date or frequency of your Direct Debit
												<%=Globals.OriginatorName%>
												will notify you 3 working days in advance of your account being debited or as otherwise agreed. If you request
												<%=Globals.OriginatorName%>
												to collect a payment, confirmation of the amount and date will be given to you at the time of the request.
											</li>
											<li>If an error is made in the payment of your Direct Debit, by
												<%=Globals.OriginatorName%>
												or by your Bank or Building Society, you are entitled to a full and immediate refund
												of the amount paid from your Bank or Building Society.
												<ul>
													<li>If you receive a refund you are not entitled to, you must pay it back when
														<%=Globals.OriginatorName%>
														asks you to.</li>
												</ul>
											</li>
											<li>You can cancel a Direct Debit at any time by simply contacting your Bank or Building
												Society. Written confirmation may be required. Please also notify us.</li>
										</ul>
									</div>
								</fieldset>
							</div>
						</div><!-- /DDMandatePreview -->
						<%If (SBP.View_CurrentPaymentType = SnapDDA.GlobalSettings.PaymentType.OneOff) Or (SBP.View_CurrentPaymentType = SnapDDA.GlobalSettings.PaymentType.RecurringCard) Or _
							(SBP.View_CurrentPaymentType = SnapDDA.GlobalSettings.PaymentType.HybridCardDD) Then %>
							<%-- Voyager. Client side form, version V1.0 --%>
							<div class="FSWrap">
								<fieldset><legend>Card Details</legend>
								<div id="CCClientSideForm" runat="server">
										<div id="CCClientSideResult"></div>
										<asp:HiddenField ID="CCClientSideFormResult" runat="server" EnableViewState="true" />
										<asp:Literal ID="litCCClientSideFormEnd" runat="server" />
										<!--<br/>-->
										<%--<input type="button" id="btnVoyagerPayment" onclick="doVoyagerTokenPayment();" value="Confirm" class="btnConfirmPaymentDisabled" Visible="false"/>--%>
								</div><!-- /CCClientSideForm -->
							</div>	
						<%End If %>
						<div class="FSWrap"><fieldset><legend>Agreement</legend>
							<div id="AgreementContainer" runat="server">
								<p>By clicking on the "Confirm" button below:</p>
								<p>
									<asp:Literal ID="litAgreement" runat="server" />
								</p>
								<p>
									<input type="checkbox" runat="server" id="AgreeCheckbox"
										onclick="EnableNextButton();"
									/>
									<label for="AgreeCheckbox">I agree to the above</label>
								</p>
							</div>
						</fieldset></div>

						
					
<p class="Buttons">
						<asp:Button ID="btnBankDetails_Next" runat="server" CssClass="btnNext_Disabled"
							CausesValidation="true" Enabled="false" ToolTip="You must agree to continue" Visible="true"
							Text="Confirm" OnClientClick="doVoyagerTokenPayment();" ValidationGroup="valBankDetails" />
						<asp:Button id="btnBankDetails_Prev" runat="server" CssClass="btnSecondary" ValidationGroup="valBankDetails" Text="Previous step" CausesValidation="false" />
						<input type="button" runat="server" id="btnVoyagerPayment" ClientIDMode="Static" onclick="if (!isAgreementChecked()) return false; doVoyagerTokenPayment();" value="Confirm" class="btnConfirmPaymentDisabled" style="display:none;"/>
					</p>

		<script type="text/javascript">
		function EnableNextButton() {	//alert("EnableNextButton");
			var next = document.getElementById("btnBankDetails_Next");
			if (!!next) {
				var cb = document.getElementById("AgreeCheckbox");
				if (!cb) {
					next.disabled = false; //	There's no checkbox,  so enable the button
				} else {
					if (cb.checked) {
						next.disabled = false;
						next.title = "Confirm";
						next.className = 'btnNext';
					} else {
						next.disabled = true;
						next.title = "You must agree to continue";
						next.className = 'btnNext_Disabled';
					}
				}
			}
		}

					function isAgreementChecked() {
				var cb = document.getElementById('AgreeCheckbox');
				if (!cb || cb.checked) return true;
				try { $('html, body').animate({ scrollTop: $('#AgreeCheckbox').offset().top - 20 }, 500); } catch (e) { }
				return false;
			}

			function setBtnState(btn, enabled, enabledClass, disabledClass, enabledTooltip, disabledTooltip) {
				if (btn) {
					if (enabled) {
						btn.disabled = false; btn.className = enabledClass; btn.title = enabledTooltip;
					} else {
						btn.disabled = true; btn.className = disabledClass; btn.title = disabledTooltip;
					}
				}
			}

			function EnableNextButton() {
				var agreed = isAgreementChecked();
				var serverBtn = document.getElementById('<%= btnBankDetails_Next.ClientID %>');
				var voyagerBtn = document.getElementById('btnVoyagerPayment');
				setBtnState(serverBtn, agreed, 'btnNext', 'btnNext_Disabled', 'Confirm', 'You must agree to continue');
				setBtnState(voyagerBtn, agreed, 'btnNext', 'btnConfirmPaymentDisabled', 'Confirm', 'You must agree to continue');
			}

function ShowHelp(id) {
			try {
				ClearHelp();
		//alert('id='+id + ' - ')
				document.getElementById(id).style.display = 'block';
				setMyIFrameHeight();
			} catch(err) {
			}
		}
		function ClearHelp() { // alert('ch');
			try {
				for (var i=0; i<ht.length; i++) {
					if (document.getElementById(ht[i])) {
						document.getElementById(ht[i]).style.display = "none";
					}
				}
			} catch(err) {
			}
		}
		var NextButtonActivated = false;
		function NextBtnClicked(ele) {			//	alert('NextBtnClicked('+ele.className+')');
			try {
				if (NextButtonActivated) {
					alert('Your application is being processed;  please wait for the screen to refresh...');
					return false;
				} else {
					document.getElementById('btnPage1Back').style.display='none';
					ele.className = 'btnNextPaymentDisabled';
					NextButtonActivated = true;
					return true;
				}
			} catch (ex) {
				alert('Caught error on NextBtnClicked');
				return false;
			}
		}

		var ht = ['helpAcctName','helpSortCode','helpAcctNum','helpCC_name','helpCC_cardType','helpCC_cardNo','helpCC_CVN','helpCC_expiryDate',
					'helpCC_startDate','helpCC_issueNo','helpCC_address','helpCC_city', 'helpCC_county', 'helpCC_country', 'helpCC_postcode'];

		/**********************
		 *	Page initialisation
		 **********************/
		ClearHelp();

		//var ht = ['helpAcctName','helpSortCode','helpAcctNum'];

		// Add a check to enable the continue button
		if(window.addEventListener){
			window.addEventListener('load',EnableNextButton, false); //W3C
		}
		else{
			window.attachEvent('onload', EnableNextButton); //IE
		}

		</script>

					</asp:View>

					<asp:View id="viewFinished" runat="server">

						<asp:Literal ID="litGoodbyeButton" runat="server" EnableViewState="false" />

						<div class="FSWrap"><fieldset class="First"><legend>Payment completed</legend><div>
		<!--
							<p><strong>Click on the Continue button to complete the process</strong></p>
		-->
							<asp:Literal ID="litThankyouMessage" runat="server" />
						</div></fieldset></div>

		<% If (Len(SFS("PINGenerator").ValueUseBlankIfNull) > 1) Then %>
			<% If (Len(SFS("AccessPIN").ValueUseBlankIfNull) > 1) Then %>
						<div class="FSWrap"><fieldset><legend>Club access PIN</legend><div>
							<p>You will need to make a note of this PIN code to be able to access the gym.</p>
							<p><b><%=SFS("AccessPIN").Value %></b></p>
							<p>The last 4 digits are your <b>Day</b> and <b>Month</b> of your birthday</p>
							<p>You are reminded that it is a condition of your membership that you do not allow anyone else to use your PIN.</p>
						</div></fieldset></div>

			<% Else %>
						<div class="FSWrap"><fieldset><legend>Club access PIN</legend><div>
							<p>Please contact the gym to get your personal PIN access code.</p>
						</div></fieldset></div>
			<% End If %>
		<% End If %>
		
		<% If (SBP.Branch.InductionBooking_System = SnapDDA.GlobalSettings.InductionBookingSystem.Fisikal) Then %>
			<% If (SFS("InductionBookingService_Status").Value = "OK") Then %>
						<div class="FSWrap"><fieldset><legend>Induction booking</legend><div>
							<p><a href="http://www.fisikal.com/sessions/widget?remote=LSFC" target="_blank">Click here to book your gym induction (http://www.fisikal.com/sessions/widget?remote=LSFC)</a>.
							You will need to enter your email address and your personal PIN code <b><%=SFS("AccessPIN").Value %></b></p>
							<p>The last 4 digits are your <b>Day</b> and <b>Month</b> of your birthday</p>
						</div></fieldset></div>

			<% Else %>
						<div class="FSWrap"><fieldset><legend>Induction booking</legend><div>
							<p>Please contact the gym to arrange for your induction session to be booked</p>
						</div></fieldset></div>
			<% End If %>
		<% End If %>

		<% Select Case SBP.View_CurrentPaymentType %>

			<% Case SnapDDA.GlobalSettings.PaymentType.DD %>

			<% Case SnapDDA.GlobalSettings.PaymentType.HybridCardDD %>
						<div class="FSWrap">
							<fieldset class="First">
								<legend>Payment confirmation</legend>
								<div>
									<p>Your first payment for
										<strong>&pound;<%=Format(SBP.DDCalculation.CalculatedFirstPaymentAmountPence / 100, "0.00") %></strong>
										has been processed from your card ending in <strong><%=Right(CC_CardNumber.Value, 4) %></strong>.</p>

									<p>Subsequent payments will be taken by Direct Debit starting on or just after
										<strong><%=Format(SBP.DDCalculation.CalculatedSecondPaymentDate, "dd MMMM yyyy")%></strong></p>

									<p>We can confirm that your Direct Debit Instruction will be set up on your bank account
										within 10 working days. The confirmation of the Instruction will be sent to your
										email address <strong><%=Me.Email.Value%></strong>
										within 3 working days or be received by you no later than 10 working days
										before the first collection.</p>

									<p>Please note your unique reference number: <strong><%=ViewState("TransRef")%></strong></p>

									<p>A receipt has been sent to your email address: <strong><%=Email.Value%></strong></p>
								</div>
							</fieldset>
						</div>

			<% Case SnapDDA.GlobalSettings.PaymentType.RecurringCard %>
						<div class="FSWrap">
							<fieldset class="First">
								<legend>Payment confirmation</legend>
								<div>
									<p>Your first payment for <strong>&pound;<%=Format(SBP.DDCalculation.CalculatedFirstPaymentAmountPence / 100, "0.00")%></strong>
										has been processed from your card ending in <strong><%=Right(CC_CardNumber.Value, 4) %></strong>.</p>

									<p>Subsequent payments will be taken from this card starting on or just after
										<strong><%=Format(SBP.DDCalculation.CalculatedSecondPaymentDate, "dd MMMM yyyy")%></strong></p>

									<p>Please note your unique reference number: <strong><%=ViewState("TransRef")%></strong></p>

									<p>A receipt has been sent to your email address: <strong><%=Email.Value%></strong></p>
								</div>
							</fieldset>
						</div>

			<% Case SnapDDA.GlobalSettings.PaymentType.OneOff %>
						<div class="FSWrap">
							<fieldset class="First">
								<legend>Payment confirmation</legend>
								<div>
									<p>Your payment of <strong>&pound;<%=Format(SBP.DDCalculation.CalculatedFirstPaymentAmountPence / 100, "0.00")%></strong>
										has been processed from your card ending in <strong><%=Right(CC_CardNumber.Value, 4) %></strong>.</p>

									<p>Please note your unique reference number: <strong><%=ViewState("TransRef")%></strong></p>

									<p>A receipt has been sent to your email address: <strong><%=Email.Value%></strong></p>
								</div>
							</fieldset>
						</div>

		<% End Select %>
		
						<div id="containerDDReceipt" runat="server" visible="false">
							<div class="FSWrap"><fieldset class="First"><legend>Direct Debit Instruction</legend><div>
								<p>That completes the setting up of your Direct Debit Instruction and we can confirm
									this will be set up on your bank account within 10 working days. The confirmation
									of the Instruction will be sent to your email address
									<strong><%=Email.Value%></strong> within 3 working days or be received
									by you no later than 10 working days before the first collection.</p>
								<p>Please note your unique reference number: <strong><%=ViewState("TransRef")%></strong></p>
							</div></fieldset></div>

							<div class="FSWrap"><fieldset><legend>Direct Debit Guarantee</legend><div>
								<img class="DDLogoAbPos" src="/clients/_MasterDocs/images/DDLogo_99x31_transparent.png" alt="Direct Debit logo" />
								<p>Please print off and retain for your records</p>
								<p>The Direct Debit Guarantee</p>
								<ul class="DDGuar">
									<li>This Guarantee is offered by all Banks and Building Societies that accept instructions to pay Direct Debits.</li>
									<li>If there are any changes to the amount, date or frequency of your Direct Debit <%=Globals.OriginatorName%> will
									notify you 3 working days in advance of your account being debited or as otherwise agreed. If you
									request <%=Globals.OriginatorName%> to collect a payment, confirmation of the amount and date will be given
									to you at the time of the request.</li>
									<li>If an error is made in the payment of your Direct Debit, by <%=Globals.OriginatorName%> or by
									your Bank or Building Society, you are entitled to a full and immediate refund of the amount paid from
									your Bank or Building Society.
										<ul><li>If you receive a refund you are not entitled to, you must pay it back when
										<%=Globals.OriginatorName%> asks you to.</li></ul>
									</li>
									<li>You can cancel a Direct Debit at any time by simply contacting your Bank or Building
									Society. Written confirmation may be required. Please also notify us.</li>
								</ul>
							</div></fieldset></div>
						</div>

						<asp:Literal EnableViewState="false" runat="server" ID="litGoodbyeButton2" />
					</asp:View>

					<asp:View ID="ERROR_PAGE" runat="server">
						<h2>Errors ocurred</h2>
						<asp:Literal runat="server" ID="litErrorMessage" EnableViewState="false" />
					</asp:View>

					<asp:View ID="viewLogon" runat="server">
						<h2>SnapDD - In-club Logon</h2>
						<asp:ValidationSummary runat="server" id="valSum_Logon" ValidationGroup="valLogon"
							HeaderText="Please correct the following errors:" CssClass="ErrorSummary" ShowMessageBox="false" />
						<p>
							Please log on using your username and password.
						</p>
						<div class="FSWrap"><fieldset><legend>SnapDD Logon</legend><div>
							<asp:Literal ID="litLogonMessages" runat="server" EnableViewState="false" />
							<p>
								<label for="UserName">Club username:</label>
								<em title="Mandatory field">*</em>
								<input id="UserName" runat="server" class="txtWide" enableviewstate="true" />
								<asp:RequiredFieldValidator runat="server" id="RequiredFieldValidator8" ControlToValidate="UserName" SetFocusOnError="true"
									CssClass="Error" Display="Dynamic" ErrorMessage="Username not entered" ValidationGroup="valLogon">Username must be entered</asp:RequiredFieldValidator>
							</p>
							<p>
								<label for="UserPass">Password:</label>
								<em title="Mandatory field">*</em>
								<input id="UserPass" runat="server" type="password" class="txtWide" enableviewstate="false" />
								<asp:RequiredFieldValidator ID="RequiredFieldValidator21" runat="server" ControlToValidate="UserPass"
									CssClass="Error" Display="Dynamic" ErrorMessage="Password not entered" SetFocusOnError="true"
									ValidationGroup="valLogon">Password must be entered</asp:RequiredFieldValidator>
							</p>
						</div></fieldset></div>

						<p class="Buttons">
							<asp:Button id="btnLogon" runat="server" CssClass="btnNext" ValidationGroup="valLogon" Text="Logon"  />
						</p>

					</asp:View>

					<asp:View ID="viewOverrides" runat="server">
						<h2>SnapDD - Product Ammendments</h2>

						<asp:Literal runat="server" ID="litOverrideErrors" Visible="false" EnableViewState="false" />

						<asp:ValidationSummary runat="server" id="valSum_Overrides" ValidationGroup="valOverrides"
							HeaderText="Please correct the following errors:" CssClass="ErrorSummary" ShowMessageBox="false" />
						<div class="FSWrap"><fieldset><legend>SnapDD Logon</legend><div>
							<p>The following values for this product can be changed:</p>
							<p>
								<label for="Override_JoiningFee">Joining fee:</label>
								<input id="Override_JoiningFee" runat="server" class="txtMedium" enableviewstate="false" />
							</p>
							<p>
								<label for="Override_ProRataAmount">
									Pro-rata payment:</label>
								<input id="Override_ProRataAmount" runat="server" class="txtMedium" enableviewstate="false" />
							</p>
							<p>
								<label for="Override_MonthlyAmount">Monthly amount:</label>
								<input id="Override_MonthlyAmount" runat="server" class="txtMedium" enableviewstate="false"  />
							</p>
							<p>
								<label for="Override_ContractLength">Contract length (months):</label>
								<input id="Override_ContractLength" runat="server" class="txtMedium" enableviewstate="false" />
							</p>
							<p>
								<label for="Override_FreePeriod">
									Free period (months):</label>
								<input id="Override_FreePeriod" runat="server" class="txtMedium" enableviewstate="false" />
							</p>
							<p>
								<label>Membership start date:</label>
								<!-- a s p Calendar ID="Override_MembershipStartDate" XXXrunat="XXXserver" enableviewstate="false" CssClass="Cal" selectionmode="Day"  / -->
								<select runat="server" id="Override_MembershipStartDate_D" class="txtNarrow" enableviewstate="False">
									<option value="1">1</option>
									<option value="2">2</option>
									<option value="3">3</option>
									<option value="4">4</option>
									<option value="5">5</option>
									<option value="6">6</option>
									<option value="7">7</option>
									<option value="8">8</option>
									<option value="9">9</option>
									<option value="10">10</option>
									<option value="11">11</option>
									<option value="12">12</option>
									<option value="13">13</option>
									<option value="14">14</option>
									<option value="15">15</option>
									<option value="16">16</option>
									<option value="17">17</option>
									<option value="18">18</option>
									<option value="19">19</option>
									<option value="20">20</option>
									<option value="21">21</option>
									<option value="22">22</option>
									<option value="23">23</option>
									<option value="24">24</option>
									<option value="25">25</option>
									<option value="26">26</option>
									<option value="27">27</option>
									<option value="28">28</option>
									<option value="29">29</option>
									<option value="30">30</option>
									<option value="31">31</option>
								</select>
								<select runat="server"	id="Override_MembershipStartDate_M" class="txtNarrow" enableviewstate="False">
									<option value="1">Jan</option>
									<option value="2">Feb</option>
									<option value="3">Mar</option>
									<option value="4">Apr</option>
									<option value="5">May</option>
									<option value="6">Jun</option>
									<option value="7">Jul</option>
									<option value="8">Aug</option>
									<option value="9">Sep</option>
									<option value="10">Oct</option>
									<option value="11">Nov</option>
									<option value="12">Dec</option>
								</select>
								<select id="Override_MembershipStartDate_Y" runat="server" class="txtNarrow Last" enableviewstate="False">
									<option value="2009">2009</option>
									<option value="2010">2010</option>
									<option value="2011">2011</option>
									<option value="2012">2012</option>
									<option value="2013">2013</option>
									<option value="2014">2014</option>
									<option value="2015">2015</option>
									<option value="2016">2016</option>
									<option value="2017">2017</option>
									<option value="2018">2018</option>
									<option value="2019">2019</option>
								</select>
							</p>
							<p>
								<label>First DD collection date:</label>
								<select runat="server" id="Override_FirstDDDate_D" class="txtNarrow" enableviewstate="False">
									<option value="1">1</option>
									<option value="2">2</option>
									<option value="3">3</option>
									<option value="4">4</option>
									<option value="5">5</option>
									<option value="6">6</option>
									<option value="7">7</option>
									<option value="8">8</option>
									<option value="9">9</option>
									<option value="10">10</option>
									<option value="11">11</option>
									<option value="12">12</option>
									<option value="13">13</option>
									<option value="14">14</option>
									<option value="15">15</option>
									<option value="16">16</option>
									<option value="17">17</option>
									<option value="18">18</option>
									<option value="19">19</option>
									<option value="20">20</option>
									<option value="21">21</option>
									<option value="22">22</option>
									<option value="23">23</option>
									<option value="24">24</option>
									<option value="25">25</option>
									<option value="26">26</option>
									<option value="27">27</option>
									<option value="28">28</option>
									<option value="29">29</option>
									<option value="30">30</option>
									<option value="31">31</option>
								</select>
								<select runat="server"	id="Override_FirstDDDate_M" class="txtNarrow" enableviewstate="False">
									<option value="1">Jan</option>
									<option value="2">Feb</option>
									<option value="3">Mar</option>
									<option value="4">Apr</option>
									<option value="5">May</option>
									<option value="6">Jun</option>
									<option value="7">Jul</option>
									<option value="8">Aug</option>
									<option value="9">Sep</option>
									<option value="10">Oct</option>
									<option value="11">Nov</option>
									<option value="12">Dec</option>
								</select>
								<select id="Override_FirstDDDate_Y" runat="server" class="txtNarrow Last" enableviewstate="False">
									<option value="2009">2009</option>
									<option value="2010">2010</option>
									<option value="2011">2011</option>
									<option value="2012">2012</option>
									<option value="2013">2013</option>
									<option value="2014">2014</option>
									<option value="2015">2015</option>
									<option value="2016">2016</option>
									<option value="2017">2017</option>
									<option value="2018">2018</option>
									<option value="2019">2019</option>
								</select>
							</p>
							<p>
								<label>
									Second DD collection date:</label>
								<!-- a s p - Calendar ID="Override_SecondDDDate" XXXrunat="XXXserver" enableviewstate="false" CssClass="Cal" selectionmode="Day"  / -->
								<select id="Override_SecondDDDate_D" runat="server" class="txtNarrow" enableviewstate="False">
									<option value="1">1</option>
									<option value="2">2</option>
									<option value="3">3</option>
									<option value="4">4</option>
									<option value="5">5</option>
									<option value="6">6</option>
									<option value="7">7</option>
									<option value="8">8</option>
									<option value="9">9</option>
									<option value="10">10</option>
									<option value="11">11</option>
									<option value="12">12</option>
									<option value="13">13</option>
									<option value="14">14</option>
									<option value="15">15</option>
									<option value="16">16</option>
									<option value="17">17</option>
									<option value="18">18</option>
									<option value="19">19</option>
									<option value="20">20</option>
									<option value="21">21</option>
									<option value="22">22</option>
									<option value="23">23</option>
									<option value="24">24</option>
									<option value="25">25</option>
									<option value="26">26</option>
									<option value="27">27</option>
									<option value="28">28</option>
									<option value="29">29</option>
									<option value="30">30</option>
									<option value="31">31</option>
								</select>
								<select id="Override_SecondDDDate_M" runat="server" class="txtNarrow" enableviewstate="False">
									<option value="1">Jan</option>
									<option value="2">Feb</option>
									<option value="3">Mar</option>
									<option value="4">Apr</option>
									<option value="5">May</option>
									<option value="6">Jun</option>
									<option value="7">Jul</option>
									<option value="8">Aug</option>
									<option value="9">Sep</option>
									<option value="10">Oct</option>
									<option value="11">Nov</option>
									<option value="12">Dec</option>
								</select>
								<select id="Override_SecondDDDate_Y" runat="server" class="txtNarrow Last" enableviewstate="False">
									<option value="2009">2009</option>
									<option value="2010">2010</option>
									<option value="2011">2011</option>
									<option value="2012">2012</option>
									<option value="2013">2013</option>
									<option value="2014">2014</option>
									<option value="2015">2015</option>
									<option value="2016">2016</option>
									<option value="2017">2017</option>
									<option value="2018">2018</option>
									<option value="2019">2019</option>
								</select>
							</p>
							<p>
								<label for="Override_ProductName">Product name:</label>
								<input id="Override_ProductName" runat="server" class="txtWide" enableviewstate="false" />
							</p>
							<p>
								<label for="Override_BranchName">Branch name:</label>
								<input id="Override_BranchName" runat="server" class="txtWide" enableviewstate="false" />
							</p>

							<p>
								Up-front payments taken In-Club against the <b>first</b> payment:
							</p>
							<p>
								<label for="UpFrontJoiningFee">Upfront joining fee:</label>
								<input id="UpFrontJoiningFee" runat="server" class="txtMedium" enableviewstate="false" />
							</p>
							<p>
								<label for="UpFrontMembershipFee">Upfront membership fee:</label>
								<input id="UpFrontMembershipFee" runat="server" class="txtMedium" enableviewstate="false" />
							</p>

						</div></fieldset></div>

						<p class="Buttons">
							<asp:Button id="btnOverrides_Submit" runat="server" CssClass="btnNext" ValidationGroup="valAgreement" Text="Save" CausesValidation="true" />
							<asp:Button id="btnOverrides_Cancel" runat="server" CssClass="btnSecondary" ValidationGroup="valAgreement" Text="Cancel" CausesValidation="false" />
							<asp:Button id="btnOverrides_RestoreDefaults" runat="server" CssClass="btnSecondary" ValidationGroup="valAgreement" Text="Restore Defaults" CausesValidation="false" />
						</p>
					</asp:View>
						<asp:View ID="OptionalPage_PreAgreement" runat="server">
						<div id="OptionalPage">
						<h2>Physical Activity Readiness Questionnaire</h2>

						<div class="FSWrap"><fieldset class="First"><legend>PARQ</legend><div>
							<p>The Questions below will indicate if you need to check with your Doctor before you start exercising.
								If you are over 69 years of age and not currently active, you should consult with your doctor
								prior to commencing an exercise regime.</p>
							<p>Please read the questions below and answer honestly:</p>
							<p id="OptionalPageErrors" class="ErrorFullWidth" style="display:none;">You must answer all questions in order to proceed</p>
							<p class="TwoRadiosOnRHS">
								<label>No <input type="radio" name="parq_DrExercise" value="no" runat="server" /></label>
								<label>Yes <input type="radio" name="parq_DrExercise" value="yes" runat="server" /></label>
								<span>Has your Doctor ever said that you have a heart condition and you should only do physical activity recommended by a doctor?</span>
							</p>
							<p class="TwoRadiosOnRHS">
								<label>No <input type="radio" name="parq_ChestPain" value="no" runat="server" /></label>
								<label>Yes <input type="radio" name="parq_ChestPain" value="yes" runat="server" /></label>
								<span>Do you feel pain in your chest when you do physical activity?</span>
							</p>
							<p class="TwoRadiosOnRHS">
								<label>No <input type="radio" name="parq_UnExplainChestPain" value="no" runat="server" /></label>
								<label>Yes <input type="radio" name="parq_UnExplainChestPain" value="yes" runat="server" /></label>
								<span>In the past month have you experienced any unexplained chest pain?</span>
							</p>
							<p class="TwoRadiosOnRHS">
								<label>No <input type="radio" name="parq_Consciousness" value="no" runat="server" /></label>
								<label>Yes <input type="radio" name="parq_Consciousness" value="yes" runat="server" /></label>
								<span>Do you lose your balance due to dizziness or do you ever lose consciousness?</span>
							</p>
							<p class="TwoRadiosOnRHS">
								<label>No <input type="radio" name="parq_Joint" value="no" runat="server" /></label>
								<label>Yes <input type="radio" name="parq_Joint" value="yes" runat="server" /></label>
								<span>Do you have a bone or joint problem that could be made worse by a change in your physical activity?</span>
							</p>

							<p class="TwoRadiosOnRHS">
								<label>No <input type="radio" name="parq_Medication" value="no" runat="server" /></label>
								<label>Yes <input type="radio" name="parq_Medication" value="yes" runat="server" /></label>
								<span>Is your doctor currently prescribing drugs for your blood pressure or heart condition?</span>
							</p>
							<p class="TwoRadiosOnRHS">
								<label>No <input type="radio" name="parq_OtherReason" value="no" runat="server" /></label>
								<label>Yes <input type="radio" name="parq_OtherReason" value="yes" runat="server" /></label>
								<span>Do you know of any other reason why you should not do physical activity?</span>
							</p>
							<p class="TwoRadiosOnRHS">
								<label>No <input type="radio" name="parq_Cholesterol" value="no" runat="server" /></label>
								<label>Yes <input type="radio" name="parq_Cholesterol" value="yes" runat="server" /></label>
								<span>Have you ever been informed that your blood cholesterol is high?</span>
							</p>
							<p class="TwoRadiosOnRHS">
								<label>No <input type="radio" name="parq_FamilyHistory" value="no" runat="server" /></label>
								<label>Yes <input type="radio" name="parq_FamilyHistory" value="yes" runat="server" /></label>
								<span>Do you have a family history of heart disease? (immediate family member less than 55 years old)</span>
							</p>
							<p class="TwoRadiosOnRHS">
								<label>No <input type="radio" name="parq_Muscle" value="no" runat="server" /></label>
								<label>Yes <input type="radio" name="parq_Muscle" value="yes" runat="server" /></label>
								<span>Do you currently have any form of muscle, joint or back injury?</span>
							</p>
							<p class="TwoRadiosOnRHS">
								<label>No <input type="radio" name="parq_Diabetes" value="no" runat="server" /></label>
								<label>Yes <input type="radio" name="parq_Diabetes" value="yes" runat="server" /></label>
								<span>Do you suffer or have you ever suffered from diabetes?</span>
							</p>
							<p class="TwoRadiosOnRHS">
								<label>No <input type="radio" name="parq_Asthma" value="no" runat="server" /></label>
								<label>Yes <input type="radio" name="parq_Asthma" value="yes" runat="server" /></label>
								<span>Do you suffer or have you ever suffered from asthma?</span>
							</p>
							<p class="TwoRadiosOnRHS">
								<label>No <input type="radio" name="parq_Bronchitis" value="no" runat="server" /></label>
								<label>Yes <input type="radio" name="parq_Bronchitis" value="yes" runat="server" /></label>
								<span>Do you suffer or have you ever suffered from bronchitis?</span>
							</p>
							<p class="TwoRadiosOnRHS">
								<label>No <input type="radio" name="parq_Epilepsy" value="no" runat="server" /></label>
								<label>Yes <input type="radio" name="parq_Epilepsy" value="yes" runat="server" /></label>
								<span>Do you suffer or have you ever suffered from epilepsy?</span>
							</p>
							<p class="TwoRadiosOnRHS">
								<label>No <input type="radio" name="parq_Conditions" value="no" runat="server" /></label>
								<label>Yes <input type="radio" name="parq_Conditions" value="yes" runat="server" /></label>
								<span>Have you any condition that may be made worse by physical exercise?</span>
							</p>
							<p class="TwoRadiosOnRHS">
								<label>No <input type="radio" name="parq_Pregnant" value="no" runat="server" /></label>
								<label>Yes <input type="radio" name="parq_Pregnant" value="yes" runat="server" /></label>
								<span>Are you pregnant?</span>
							</p>
							<p class="FullWidthTextArea">
								<label for="parq_Details">If you have answered <b>Yes</b> to any of the above,  please provide details:</label>
								<textarea id="parq_Details" name="parq_Details" placeholder="Details" runat="server"></textarea>
							</p>

						</div></fieldset></div>

						<div class="FSWrap"><fieldset class="First"><legend>PAR-Q Agreement</legend><div>
							<p id="OptionalPageAgreement" class="ErrorFullWidth" style="display: none;">You must agree to this statement in order to continue</p>
							<p class="FullWidthCheckboxLHS">
								<label>
									<input id="parq_Agreement" name="parq_Agreement" value="true" type="checkbox" runat="server" />
									I agree
								</label>

								I have read, understood and completed the above questionnaire and acknowledge that there are risks and dangers inherent in physical exercise
								and duly undertake the activity at my own risk. Any liability on the part of the operators is excluded unless negligence can be proven. I agree
								to observe the rules and conditions of membership. I also acknowledge that I must not use any piece of equipment for which I have not been shown
								how to use by an instructor. I confirm that the information that I have provided is correct at this time and should I become aware of any relevant
								changes to my health or condition, I will inform an instructor.
							</p>
						</div></fieldset></div>

						<p class="Buttons">
							<asp:Button id="btnOptionalPage_Next" OnClientClick="return ValidateQuestions()" runat="server" CssClass="btnNext" Text="Next step" />
							<asp:Button id="btnOptionalPage_Prev" runat="server" CssClass="btnSecondary" Text="Previous step" />
						</p>
		<script type="text/javascript">
		function ValidateQuestions(){
			try {
		// TBD...
		//		var fieldList = ['parq_DrExercise', 'parq_ChestPain', 'parq_Medication', 'parq_HighBP', 'parq_Consiousness', 'parq_BoneJoint', 'parq_OtherReason'];
		//		for (var i = 0; i < fieldList.length; i++) {
		//			document.getElementById('OptionalPageErrors').style.display = "block";
		//		}
				//	inspect each Radio button to see if it's sibling's checked

		/*
				var fieldList = ['parq_DrExercise', 'parq_ChestPain', 'parq_Medication', 'parq_HighBP', 'parq_Consiousness', 'parq_BoneJoint', 'parq_OtherReason'];
				for (var i=0; i<fieldList.length; i++){
					if($("input:radio[name=" + fieldList[i] + "]:checked").length == 0) {
						document.getElementById('OptionalPageErrors').style.display = "block";
						$('html, body').animate({
							scrollTop: $('#OptionalPageErrors').offset().top
						}, 200);
						return false;
					}
				}
		*/
		/*
				$("input:radio").each(function(){
					var name = $(this).attr("name");
					if($("input:radio[name="+name+"]:checked").length == 0) {
						document.getElementById('OptionalPageErrors').style.display = "block";
						$('html, body').animate({
							scrollTop: $('#OptionalPageErrors').offset().top
						}, 200);
						return false;
					}
				});
		*/

				//	Build a list of radio groups
				var radioGroups = new Object();
				$(":radio").each(function(){
					radioGroups[this.name] = true;
				});

				for(groupName in radioGroups){
		//alert('group='+groupName);
					var isChecked = !!$(":radio[name=" + groupName + "]:checked").length;
		//			alert(groupName+(isChecked?' has checked radios':' does not have checked radios'));
					if (!isChecked) {
						document.getElementById('OptionalPageErrors').style.display = "block";
						$('html, body').animate({
							scrollTop: $('#OptionalPageErrors').offset().top
						}, 200);
						return false;
					}
				}
				document.getElementById('OptionalPageErrors').style.display = "none";

				//	Check if the checkbox isn't checked...
				if (!$('#parq_Agreement').is(':checked')) {
						document.getElementById('OptionalPageAgreement').style.display = "block";
						$('html, body').animate({
							scrollTop: $('#OptionalPageAgreement').offset().top
						}, 200);
						return false;
				}
				document.getElementById('OptionalPageAgreement').style.display = "none";

				return true;

			} catch(err) {
				alert("Oops: " + err);
			}
		}
		</script>
						</div>
					</asp:View>
				
        <asp:View ID="view3DSecure" runat="server">
            <div class="FSWrap"><fieldset class="First"><legend><%=GetTranslation("view3DSecure_Legend")%></legend>
                <div>
                    <input id="ThreeDReturnValue" name="z" type="hidden" />
                    <input id="ThreeDReturnMD" name="MD" type="hidden" />
                    <input id="ThreeDReturnPaRes" name="PaRes" type="hidden" />
                    <script type="text/javascript">
                        function ThreeDPostback(postbackValue, postbackMD, postbackPaRes) {
                            try {
                                document.getElementById('ThreeDReturnValue').value = postbackValue;
                                document.getElementById('ThreeDReturnMD').value = postbackMD;
                                document.getElementById('ThreeDReturnPaRes').value = postbackPaRes;
                                document.getElementById('Signup').submit();
                            } catch(e) {
                                // Write error to screen
                                var sErr = '<h1><%=GetTranslation("ErrorMessage_Legend", True)%></h1>';
                                sErr += '<p><%=GetTranslation("ErrorMessage_Message", True)%></p>';
                                if (true) {
                                    sErr += '<p>' + e.message + '</p>';
                                }
                                var ele = document.getElementById('CallbackErrorMessagePlaceholder')
                                ele.innerHTML = sErr;
                                ele.style.display = 'block';
                            }
                        }
                    </script>

                    <div id="CallbackErrorMessagePlaceholder" style="display: none;">
                    </div>

                    <div id="ThreeDIFrameContainer"><!--	container required for iOS	-->
                    <asp:Literal ID="litCC3DSecureContents" runat="server"/>
                    </div>

                </div></fieldset></div>
        </asp:View>

        </asp:MultiView></div>

				<SnapControl:SnapSignupTabsControl_v2	runat="server" id="TabsControl2"
					Type="Footer"
					P1_Branch = "Branch Selection"
					P1_Branch_Complete = "Branch Selection - Complete"
			  P2_Product = "Membership Selection"
			  P2_Product_Complete = "Membership Selection"
				/>

			</div>

			<div id="SnapInfo">
		<% If Me.Globals.RequireLogon Then %>
				<h2>Club Logon</h2>
				<hr />
			<% If LoggedOnUser IsNot Nothing Then%>
						<p>Logged on as:<br /><b><%=LoggedOnUser.DescriptionLong() %></b></p>
						<p><asp:Button runat="server" TabIndex="-1" ID="btnLogoff" Text="Logoff" /></p>
			<% End If %>
		<% End If %>
				<asp:MultiView ID="BranchInfoPanel" runat="server" EnableViewState="false">
					<!-- BRANCH VIEWS: generated by dfcmigrate -->
				</asp:MultiView>
				<% If Not String.IsNullOrEmpty(Globals.TermsAndConditionsDocument) Then %>
				<p><a href="<%=Globals.TermsAndConditionsDocument %>" target="_blank">Download the Membership Terms &amp; Conditions</a></p>
				<% End If %>
				<p class="Snap">
					<a href="https://www.snapdda.co.uk" target="_blank">
						<img alt="Xplorpay - The smarter way to pay" src="Images/Xplor_Pay_Logo_67x37.png"
							title="Xplorpay - The smarter way to pay" /></a>
					<a href="https://www.snapdda.co.uk" target="_blank">Xplorpay - the smarter way to pay</a>
				</p>
			</div><!-- /SnapInfo -->
		<% If (MultiView1.ActiveViewIndex = ViewPage.Finished) Then Response.Write("<form action='#'>") %></form></div>
			<p class="Footer">
				Powered by <a href="https://www.snapdda.co.uk">Xplorpay</a><br />
				Copyright 2007-<%= Date.Today.Year %> &copy; Debit Finance Collections PLC.</p>
		 </div>
		 
		 <div id="LBMask">
			<div id="LBContent">Processing.... please wait</div>
		</div>
		<script type="text/javascript">
			var gaJsHost = (("https:" == document.location.protocol) ? "https://ssl." : "http://www.");
			document.write(unescape("%3Cscript src='" + gaJsHost + "google-analytics.com/ga.js' type='text/javascript'%3E%3C/script%3E"));
		</script>
		<script type="text/javascript">
			var pageTracker = _gat._getTracker("UA-2184073-2");
			pageTracker._trackPageview();
		</script>
		<!--	START IFrame resize	--><script type="text/javascript">
//  IFrame resizing.  Requires code in the calling parent page.
	if (typeof console == "undefined" || typeof console.log == "undefined") var console = { log: function() { } };

	var H_initFlag = true;

	function setMyIFrameHeight() {				console.log("iframe.setMyIFrameHeight");
		try {
			var sizer = document.getElementById("FormContainer") || document.getElementById("MainContainer");
			var h = sizer ? sizer.clientHeight : 0;
			console.log("Height = " + h);
			if(		(h==0)										// IE 6 & 7
					|| (navigator.userAgent.match(/iPhone/i))	// iPhone
					|| (navigator.userAgent.match(/iPod/i))		// iPod
				) {
					h = document.body.scrollHeight;
			}
			h += 100;	//	Add a few px to the height
			parent.postMessage("Harlands_setIFrameHeight|" + h, "*");

/*  Bootstrap workaround - Bootstrap breaks this resizing code,  so add this line to remove the width on the
 *  IFrame which then forces Bootstrap to re-calculate the element widths.  Hmm.
 *  Comment out/remove this line for non-Bootstrap implementations.
 */
//			parent.postMessage("Harlands_removeIFrameWidth", "*");

			if (H_initFlag) {
				H_initFlag = false;
console.log("CALLING Harlands_windowScrollTo...")
			//	parent.postMessage('Harlands_scrollIntoView|IFrameWrapper', '*');
			    parent.postMessage('Harlands_windowScrollTo|0|0', '*');
			}
		} catch (err) {
			console.log("setMyIFrameHeight - caught error: " + err.message);
		}
	}

	function clientPostMessageHandler(e) {			console.log('CLIENT received message');
		if (e.data == 'setHeight') {
			setMyIFrameHeight();
		}
	}

	/*	Initialise message handler - IE safe     */
	if (!!window.addEventListener) {
		window.addEventListener('message', clientPostMessageHandler, false);
	} else if (window.attachEvent) {
		window.attachEvent('onmessage', clientPostMessageHandler);
	}


	//	Initialise the resizing utilities in the parent page
	if (!!parent.postMessage) {
		parent.postMessage('Harlands_init|SnapDDAIFrame|IFrameWrapper|1000', '*');
	}

	</script><!--	END IFrame resize	-->	
	</body>
</html>