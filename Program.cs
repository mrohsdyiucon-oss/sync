
using Newtonsoft.Json;
using RestSharp;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// =====================
// HARD CODED SECRETS
// =====================
string hardcodedJwtSecret = "THIS_IS_SUPER_SECRET_KEY_123456";
string hardcodedPassword = "AdminPassword123!";
string awsKey = "AKIAFAKEKEY123456";
string connectionString =
    "Server=localhost;Database=ProdDb;User Id=sa;Password=SuperPassword123;TrustServerCertificate=true";

// =====================
// HEALTH
// =====================
app.MapGet("/", () => "Vulnerable API Running");
app.MapGet("/health", () => "OK");

// =====================
// SQL INJECTION (1)
// =====================
app.MapGet("/users", async (string username) =>
{
    using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    string sql = $"SELECT * FROM Users WHERE Username = '{username}'";

    using var command = new SqlCommand(sql, connection);
    using var reader = await command.ExecuteReaderAsync();

    var results = new List<object>();

    while (await reader.ReadAsync())
    {
        results.Add(new
        {
            Id = reader["Id"],
            Username = reader["Username"]
        });
    }

    return Results.Ok(results);
});

// =====================
// SQL INJECTION (2)
// =====================
app.MapGet("/search", (string q) =>
{
    string sql = "SELECT * FROM Products WHERE Name LIKE '%" + q + "%'";
    return Results.Ok(sql);
});

// =====================
// COMMAND INJECTION
// =====================
app.MapGet("/ping", (string host) =>
{
    var process = new Process();
    process.StartInfo.FileName = "cmd.exe";
    process.StartInfo.Arguments = $"/c ping {host}";
    process.StartInfo.RedirectStandardOutput = true;
    process.Start();

    return Results.Text(process.StandardOutput.ReadToEnd());
});

// =====================
// SSRF (1)
// =====================
app.MapGet("/fetch", async (string url) =>
{
    var client = new RestClient(url);
    var response = await client.ExecuteAsync(new RestRequest());
    return Results.Text(response.Content ?? "");
});

// =====================
// SSRF (2)
// =====================
app.MapGet("/cloud", async (string url) =>
{
    var client = new HttpClient();
    return await client.GetStringAsync(url);
});

// =====================
// PATH TRAVERSAL
// =====================
app.MapGet("/file", (string path) =>
{
    var content = File.ReadAllText(path);
    return Results.Text(content);
});

// =====================
// WEAK CRYPTO
// =====================
app.MapGet("/md5", (string value) =>
{
    using var md5 = MD5.Create();
    var bytes = Encoding.UTF8.GetBytes(value);
    return Convert.ToHexString(md5.ComputeHash(bytes));
});

// =====================
// XXE
// =====================
app.MapPost("/xml", async (HttpRequest request) =>
{
    using var reader = new StreamReader(request.Body);
    string xml = await reader.ReadToEndAsync();

    XmlDocument doc = new XmlDocument();
    doc.XmlResolver = new XmlUrlResolver(); // dangerous
    doc.LoadXml(xml);

    return Results.Ok(doc.InnerText);
});

// =====================
// DESERIALIZATION (Newtonsoft)
// =====================
app.MapPost("/deserialize", async (HttpRequest request) =>
{
    using var reader = new StreamReader(request.Body);
    string body = await reader.ReadToEndAsync();

    var obj = JsonConvert.DeserializeObject<object>(
        body,
        new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All
        });

    return Results.Ok(obj);
});

// =====================
// XML DESERIALIZATION (XmlSerializer unsafe usage pattern)
// =====================
app.MapPost("/xml2", (string xml) =>
{
    XmlSerializer serializer = new XmlSerializer(typeof(object));
    using var reader = new StringReader(xml);
    return serializer.Deserialize(reader);
});

// =====================
// OPEN REDIRECT
// =====================
app.MapGet("/redirect", (string url) =>
{
    return Results.Redirect(url);
});

// =====================
// INFORMATION DISCLOSURE
// =====================
app.MapGet("/debug", () =>
{
    return Results.Ok(new
    {
        Password = hardcodedPassword,
        JwtSecret = hardcodedJwtSecret,
        AwsKey = awsKey,
        ConnectionString = connectionString,
        MachineName = Environment.MachineName,
        User = Environment.UserName,
        Dir = Environment.CurrentDirectory
    });
});

// =====================
// AUTH BYPASS
// =====================
app.MapGet("/admin", (string role) =>
{
    if (role != "admin")
        return Results.Unauthorized();

    return Results.Ok("Admin Panel");
});

// =====================
// LOGIC BUG
// =====================
app.MapGet("/discount", (int price) =>
{
    if (price > 0)
        return Results.Ok(price * -1);

    return Results.Ok(price);
});

// =====================
// REFLECTION ABUSE
// =====================
app.MapGet("/type", (string type) =>
{
    var t = Type.GetType(type);
    return Results.Ok(t?.FullName);
});

// =====================
// FILE UPLOAD (unsafe)
// =====================
app.MapPost("/upload", async (HttpRequest request) =>
{
    var file = request.Form.Files[0];
    var path = Path.Combine("uploads", file.FileName);

    using var stream = File.Create(path);
    await file.CopyToAsync(stream);

    return Results.Ok(path);
});

// =====================
// ENV DISCLOSURE
// =====================
app.MapGet("/env", () =>
{
    return Results.Ok(Environment.GetEnvironmentVariables());
});

app.Run();
