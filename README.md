# Staybnb

Staybnb is a managed property rental platform inspired by Airbnb, built with **ASP.NET Core MVC**, **Entity Framework Core** and **SQL Server** for the Programming 742 assignment at Richfield.

The project follows the full user lifecycle of a rental marketplace. A user registers as a **Guest**, applies to become a **Host** by submitting a property with photos, and is moved to the Host role once an **Admin** approves the application. A seeded **SuperAdmin** oversees the platform and is the only account that can promote Guests to Admins.

---

## Features

### Guest
- Browse active listings in an Airbnb-style gallery
- Filter by city, property type, number of guests, price range and dates
- Live price quote: `TotalPrice = (PricePerNight × Nights) + CleaningFee + ServiceFee`
- Request, view and cancel bookings
- Multi-step online check-in for approved bookings, including an ID or passport upload
- Wishlist, and reviews after a completed stay
- Apply to become a Host ("Become a Host" flow, at least one photo required)

### Host
- Dashboard with properties, bookings, earnings and average rating
- Manage listings: edit details, manage photos, choose a cover photo, toggle `IsActive`
- Approve or reject booking requests (double bookings are blocked)
- Configure each property's check-in process (steps, instructions, required documents)
- Verify or reject guest documents and complete check-ins
- Apply to list additional properties

### Admin
- Dashboard with platform totals, recent activity, applications and bookings
- Review host applications (approve with notes or reject with a reason)
- Oversee all bookings, and suspend or reactivate properties
- System audit: filter, page through and export the activity log as CSV

### SuperAdmin
- Everything an Admin can do
- User management: promote Guests to Admin and revoke Admin rights

### Everyone (signed in)
- Database-backed inbox for messages, with read and unread tracking
- Notification centre (bell icon) with read/unread, mark all as read and delete

---

## Booking lifecycle

```
Pending ──► Approved ──► CheckedIn ──► Completed
   │            │
   ├──► Rejected (Host)
   └────────────┴──► Cancelled (Guest)
```

Each status change is saved, written to the activity log and notified to the other party.

---

## Tech stack

| Area | Technology |
|---|---|
| Framework | ASP.NET Core MVC on .NET 10 (C#) |
| Data access | Entity Framework Core 10 (code-first, migrations) |
| Database | SQL Server LocalDB (`StaybnbDb`) |
| Authentication | ASP.NET Core Identity with roles |
| Front end | Razor views, Bootstrap 5, Bootstrap Icons, vanilla JavaScript |

NuGet packages:

- `Microsoft.EntityFrameworkCore`
- `Microsoft.EntityFrameworkCore.SqlServer`
- `Microsoft.EntityFrameworkCore.Design`
- `Microsoft.EntityFrameworkCore.Tools`
- `Microsoft.AspNetCore.Identity.EntityFrameworkCore`
- `Microsoft.AspNetCore.Identity.UI`
- `Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore`
- `System.Security.Cryptography.Pkcs`, referenced directly to upgrade a vulnerable dependency (see the problem fixes below)

---

## Getting started

### Prerequisites
- Visual Studio 2026 (or a version that supports .NET 10) with the **ASP.NET and web development** workload
- .NET 10 SDK
- SQL Server LocalDB (installed with Visual Studio)

### Setup
1. Open `Staybnb.slnx` in Visual Studio.
2. Restore the NuGet packages. Visual Studio usually does this automatically.
3. Open **Tools → NuGet Package Manager → Package Manager Console** and run:
   ```powershell
   Update-Database
   ```
   If the `Migrations` folder is missing, run `Add-Migration InitialCreate` first.
4. Press **F5** to run the app.

On start-up the `RoleSeeder` applies any outstanding migrations and creates:
- the roles **Guest**, **Host**, **Admin** and **SuperAdmin**
- the SuperAdmin account
- 12 amenities (seeded with `HasData`)

No SQL scripts are needed.

### Default login

| Role | Email | Password |
|---|---|---|
| SuperAdmin | `superadmin@staybnb.co.za` | `SuperAdmin@123` |

Register a new account to start as a Guest. Passwords need at least 6 characters, with an uppercase letter, a lowercase letter and a digit.

### Trying the full flow
1. Register as a Guest and submit a host application with at least one photo.
2. Log in as the SuperAdmin (or an Admin) and approve the application. The user becomes a Host.
3. Register a second Guest and book the new property.
4. As the Host, approve the booking.
5. As the Guest, complete the online check-in and upload an ID.
6. As the Host, verify the document and complete the check-in. The booking becomes **CheckedIn**.

---

## Project structure

```
Staybnb/
├── Controllers/            MVC controllers (+ Api/PropertiesApiController for the price quote)
├── Data/
│   ├── ApplicationDbContext.cs
│   └── Seeders/RoleSeeder.cs
├── Migrations/             EF Core migrations
├── Models/
│   ├── Common/             ServiceResult, PriceBreakdown, display helpers
│   ├── Constants/          Role names and activity action names
│   ├── Entities/           Database entities (HostProperty, Booking, GuestCheckIn, ...)
│   ├── Enums/              Status enums (BookingStatus, ApplicationStatus, ...)
│   ├── Settings/           StaybnbSettings (bound from appsettings.json)
│   └── ViewModels/         Account, Admin, Guest, Host, Shared
├── Services/               Business logic (+ Interfaces/)
├── Views/                  Razor views, with separate layouts for guests, hosts and admins
├── wwwroot/
│   ├── css/staybnb.css     App styles (main colour set by --stb-primary)
│   ├── js/                 booking.js (live quote), apply-host.js (photo previews)
│   └── uploads/            Property photos (created at runtime)
├── App_Data/PrivateUploads Guest ID documents, outside wwwroot (created at runtime)
├── Program.cs
└── appsettings.json
```

---

## Configuration

Settings in `appsettings.json`:

| Key | Default | Meaning |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | LocalDB `StaybnbDb` | Database connection |
| `SuperAdmin:Email` / `Password` | see above | Seeded SuperAdmin account |
| `Staybnb:ServiceFeePercentage` | `0.12` | Service fee (12% of the nightly subtotal) |
| `Staybnb:MinNights` / `MaxNights` | `1` / `30` | Allowed length of stay |
| `Staybnb:MaxDaysInAdvance` | `365` | How far ahead guests can book |
| `Staybnb:MaxImageSizeMb` | `5` | Maximum size per property photo |
| `Staybnb:MaxDocumentSizeMb` | `10` | Maximum size per guest document |
| `Staybnb:MaxImagesPerProperty` | `10` | Maximum photos per property |

---

## Security notes
- Pages are protected with `[Authorize(Roles = ...)]`. Guest and Host are mutually exclusive roles, so a Host cannot open Guest pages and the reverse.
- Ownership is checked too. For example, a Host can only manage their own bookings.
- All POST forms use anti-forgery tokens.
- Uploads are checked for size, extension and real file content (magic bytes), and are saved under random names.
- Guest ID documents are stored outside `wwwroot` and are only served to the guest, the property's Host or an Admin.
- An account is locked for 5 minutes after 5 failed logins.

---

## Fixes for common problems

| Problem | Fix |
|---|---|
| `Invalid object name 'AspNetRoles'` | No migration was applied. Run `Add-Migration InitialCreate` (if needed), then `Update-Database`. |
| `Add-Migration` stops on warning NU1902 | Make sure `System.Security.Cryptography.Pkcs` is referenced in `Staybnb.csproj`, then rebuild. |
| Changes to a file don't show up | Visual Studio may have saved an old open tab over the file. Close the tab, choose **Reload All**, rebuild, then press Ctrl+F5 in the browser. |
| A newly approved Host still sees Guest pages | Refresh the page. Roles are re-read on every request (`ValidationInterval = 0`). |
| Upload fails for large photo sets | The request limit is 60 MB (10 photos × 5 MB). Upload fewer or smaller files. |

---

## Author
Dihan Visagie
Github: 
