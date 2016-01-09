puts 'Creating teams...'

Team.create! abbreviation: 'NJD', city: 'Newark', name: 'Devils', state: 'New Jersey', full_name: 'New Jersey Devils', division_id: Enums::Division::METRO
Team.create! abbreviation: 'NYI', city: 'New York', name: 'Islanders', state: 'New York', division_id: Enums::Division::METRO
Team.create! abbreviation: 'NYR', city: 'New York', name: 'Rangers', state: 'New York', division_id: Enums::Division::METRO
Team.create! abbreviation: 'WSH', city: 'Washington', name: 'Capitals', state: 'D.C.', division_id: Enums::Division::METRO
Team.create! abbreviation: 'CBJ', city: 'Columbus', name: 'Blue Jackets', state: 'Ohio', division_id: Enums::Division::METRO
Team.create! abbreviation: 'PHI', city: 'Philadelphia', name: 'Flyers', state: 'Pennsylvania', full_name: 'Philadelphia Flyers', division_id: Enums::Division::METRO
Team.create! abbreviation: 'PIT', city: 'Pittsburg', name: 'Penguins', state: 'Pennsylvania', full_name: 'Pittsburg Penguins', division_id: Enums::Division::METRO
Team.create! abbreviation: 'CAR', city: 'Raleigh', name: 'Hurricanes', state: 'North Carolina', full_name: 'Carolina Hurricanes', division_id: Enums::Division::METRO

Team.create! abbreviation: 'OTT', city: 'Ottowa', name: 'Senators', state: 'Ontario', division_id: Enums::Division::ATLANTIC
Team.create! abbreviation: 'TBL', city: 'Tampa Bay', name: 'Lightning', state: 'Florida', division_id: Enums::Division::ATLANTIC
Team.create! abbreviation: 'TOR', city: 'Toronto', name: 'Maple Leafs', state: 'Ontario', division_id: Enums::Division::ATLANTIC
Team.create! abbreviation: 'DET', city: 'Detroit', name: 'Red Wings', state: 'Michigan', division_id: Enums::Division::ATLANTIC
Team.create! abbreviation: 'BOS', city: 'Boston', name: 'Bruins', state: 'Massachusetts', division_id: Enums::Division::ATLANTIC
Team.create! abbreviation: 'BUF', city: 'Buffalo', name: 'Sabres', state: 'New York', division_id: Enums::Division::ATLANTIC
Team.create! abbreviation: 'MTL', city: 'Montreal', name: 'Canadiens', state: 'Quebec', division_id: Enums::Division::ATLANTIC
Team.create! abbreviation: 'FLA', city: 'Sunrise', name: 'Panthers', state: 'Florida', full_name: 'Florida Panthers', division_id: Enums::Division::ATLANTIC

Team.create! abbreviation: 'ANA', city: 'Anaheim', name: 'Ducks', state: 'California', division_id: Enums::Division::PACIFIC
Team.create! abbreviation: 'ARI', city: 'Phoenix', name: 'Coyotes', state: 'Arizona', full_name: 'Arizona Coyotes', division_id: Enums::Division::PACIFIC
Team.create! abbreviation: 'SJS', city: 'San Jose', name: 'Sharks', state: 'California', division_id: Enums::Division::PACIFIC
Team.create! abbreviation: 'VAN', city: 'Vancouver', name: 'Canucks', state: 'British Columbia', division_id: Enums::Division::PACIFIC
Team.create! abbreviation: 'CGY', city: 'Calgary', name: 'Flames', state: 'Alberta', division_id: Enums::Division::PACIFIC
Team.create! abbreviation: 'LAK', city: 'Los Angeles', name: 'Kings', state: 'California', division_id: Enums::Division::PACIFIC
Team.create! abbreviation: 'EDM', city: 'Edmonton', name: 'Oilers', state: 'Alberta', division_id: Enums::Division::PACIFIC

Team.create! abbreviation: 'CHI', city: 'Chicago', name: 'Blackhawks', state: 'Illinois', division_id: Enums::Division::CENTRAL
Team.create! abbreviation: 'COL', city: 'Denver', name: 'Avalanche', state: 'Colorado', full_name: 'Colorado', division_id: Enums::Division::CENTRAL
Team.create! abbreviation: 'DAL', city: 'Dallas', name: 'Stars', state: 'Texas', division_id: Enums::Division::CENTRAL
Team.create! abbreviation: 'STL', city: 'St. Louis', name: 'Blues', state: 'Missouri', division_id: Enums::Division::CENTRAL
Team.create! abbreviation: 'ANA', city: 'Winnipeg', name: 'Jets', state: 'Manitoba', division_id: Enums::Division::CENTRAL
Team.create! abbreviation: 'MIN', city: 'St. Paul', name: 'Wild', state: 'Minnesota', full_name: 'Minnesota Wild', division_id: Enums::Division::CENTRAL
Team.create! abbreviation: 'NSH', city: 'Nashville', name: 'Predators', state: 'Tennessee', division_id: Enums::Division::CENTRAL

puts '[x] Done'
