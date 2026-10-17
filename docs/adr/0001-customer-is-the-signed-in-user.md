# The Customer is the signed-in User

An Order's Customer is always the User who places it, and the API reads the Customer id from that User's token instead of the request body. Before this, `POST /orders` accepted any `CustomerId`, so a client could place Orders for, and read Orders of, anyone. We chose this over letting staff place Orders on someone else's behalf, because the web app only has self-service shoppers. If staff-placed Orders are ever needed, it should be an explicit Admin capability, not a client-supplied id.
