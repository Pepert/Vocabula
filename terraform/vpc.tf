# Dedicated VPC.
#
# The Fargate task runs in public subnets with a public IP (outbound via the
# internet gateway), which avoids a NAT gateway (~$32/month). RDS runs in
# private subnets and never needs outbound internet access.

resource "aws_vpc" "vocabula" {
  cidr_block           = "10.0.0.0/16"
  enable_dns_hostnames = true
  enable_dns_support   = true

  tags = {
    name = "vocabula"
  }
}

data "aws_availability_zones" "available" {
  state = "available"
}

resource "aws_internet_gateway" "vocabula" {
  vpc_id = aws_vpc.vocabula.id

  tags = {
    name = "vocabula"
  }
}

resource "aws_subnet" "public" {
  count = 2

  vpc_id                  = aws_vpc.vocabula.id
  availability_zone       = data.aws_availability_zones.available.names[count.index]
  cidr_block              = cidrsubnet(aws_vpc.vocabula.cidr_block, 8, count.index)
  map_public_ip_on_launch = true

  tags = {
    name = "vocabula-public-${count.index}"
  }
}

resource "aws_subnet" "private" {
  count = 2

  vpc_id            = aws_vpc.vocabula.id
  availability_zone = data.aws_availability_zones.available.names[count.index]
  cidr_block        = cidrsubnet(aws_vpc.vocabula.cidr_block, 8, 10 + count.index)

  tags = {
    name = "vocabula-private-${count.index}"
  }
}

resource "aws_route_table" "public" {
  vpc_id = aws_vpc.vocabula.id

  route {
    cidr_block = "0.0.0.0/0"
    gateway_id = aws_internet_gateway.vocabula.id
  }

  tags = {
    name = "vocabula-public"
  }
}

resource "aws_route_table_association" "public" {
  count = 2

  subnet_id      = aws_subnet.public[count.index].id
  route_table_id = aws_route_table.public.id
}

# Route table for the private subnets (RDS). It normally carries no routes
# beyond the implicit VPC-local one. When allow_migrations_from is set, a
# temporary default route to the internet gateway is added so the RDS public
# IP becomes reachable from the developer machine; publicly_accessible alone
# does not work from subnets without an internet route.
resource "aws_route_table" "private" {
  vpc_id = aws_vpc.vocabula.id

  tags = {
    name = "vocabula-private"
  }
}

resource "aws_route" "migrations" {
  count = var.allow_migrations_from != "" ? 1 : 0

  route_table_id         = aws_route_table.private.id
  destination_cidr_block = "0.0.0.0/0"
  gateway_id             = aws_internet_gateway.vocabula.id
}

resource "aws_route_table_association" "private" {
  count = 2

  subnet_id      = aws_subnet.private[count.index].id
  route_table_id = aws_route_table.private.id
}
