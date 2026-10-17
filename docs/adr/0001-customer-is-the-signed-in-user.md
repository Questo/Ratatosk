# The Customer is the signed-in User

An Order's Customer is always the User who places it, and the API reads the Customer id from that User's token instead of the request body. Before this, `POST /orders` accepted any `CustomerId`, so a client could place Orders for, and read the Orders of, anyone. The web app is an Admin-only back office that watches Orders and never places them, so no client needs to act on a Customer's behalf. If Admins ever need to place Orders for someone else, that should be an explicit Admin capability with its own way of choosing a Customer, not a client-supplied id.
