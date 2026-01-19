# Project Description
Auction is a real-time online auction platform built with ASP.NET Core 8.0 MVC.
# Key Features:
User Authentication — JWT-based login system with role management and password security
Auction Management — Create, edit, and browse auction listings with support for multiple images
Bidding System — Place bids on active items with validation (must exceed minimum price)
Real-Time Updates — Uses SignalR (AuctionHub) to push live bid updates to all connected users viewing an auction
Winner Determination — Automatically identifies the highest bidder when an auction ends
# Tech Stack:
Layer	Technology
Backend	ASP.NET Core 8.0, Entity Framework Core
Frontend	Razor Views, Bootstrap, jQuery
Real-time	SignalR
Database	SQL Server (via EF Core migrations)
Auth	JWT tokens
# Core Entities:
User — Registered users with roles
AuctionItem — Items being auctioned (with start/end time, minimum price)
Bid — User bids linked to auction items
AuctionImage — Multiple images per auction listing
This is a fully functional bidding platform suitable for hosting timed auctions with live competition among participants.
# To Run The Project:
Make Sure Of Connection Strings in AppSettings.json 
Open NuGet Console
run: "update-database"
the app is ready to be live
