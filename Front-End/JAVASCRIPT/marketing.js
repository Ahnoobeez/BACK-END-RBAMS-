// =========================
// MARKETING PAGE
// =========================

// Initialize Marketing page components
function initializeMarketingPage() {

    const btn = document.querySelector(".btn-primary");

    if (btn) {
        btn.addEventListener("click", function () {
            alert("Generate Quotation clicked!");
        });
    }

}


// Switch between Marketing tabs
function switchTab(event, sectionId) {

    event.preventDefault();

    const links = document.querySelectorAll(".tab-nav a");

    links.forEach(link => {
        link.classList.remove("active");
    });

    event.currentTarget.classList.add("active");

    const contents = document.querySelectorAll(".tab-content");

    contents.forEach(content => {
        content.classList.remove("active-content");
        content.classList.add("hidden");
    });

    const target = document.getElementById(sectionId);

    if (target) {

        target.classList.remove("hidden");
        target.classList.add("active-content");

        // Load client records when Saved Records is opened
        if (sectionId === "savedRecordsSection") {
            loadClientsIntoTable();
        }
    }
}

async function loadClientsIntoTable(event, sectionId) {

    const tableBody = document.getElementById("clientsTableBody");

    if (!tableBody) {
        console.error("clientsTableBody not found.");
        return;
    }

    try {

        const clients = await getClients();

        tableBody.innerHTML = "";

        clients.forEach(client => {

            const row = document.createElement("tr");

            row.innerHTML = `
                <td>${client.client_ID}</td>
                <td>${client.client_Name ?? ""}</td>
                <td>${client.client_Telephone ?? ""}</td>
                <td>${client.client_Address ?? ""}</td>
                <td>${client.client_TinNumber ?? ""}</td>
                <td>${client.client_PaymentTerms ?? ""}</td>
                <td>

                    <button onclick="editClient(${client.client_ID})">
                        Edit
                    </button>
                    <button onclick="deleteClient(${client.client_ID})">
                        Delete
                    </button>
                </td>
            `;

            tableBody.appendChild(row);
        });

    } catch (error) {

        console.error("Failed to load clients:", error);

    }
}
