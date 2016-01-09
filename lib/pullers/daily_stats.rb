require 'open-uri'
require 'time'

module Pullers
    class DailyStats

        def self.run(date = Date.today - 1.day)

            # construct url for date
            url = "http://www.hockey-reference.com/friv/dailyleaders.cgi?month=#{date.month}&day=#{date.day}&year=#{date.year}"

            Rails.logger.info '#####'
            Rails.logger.info "##### Pullers::DailyStats => Running for #{date}"
            Rails.logger.info "##### Pullers::DailyStats => Opening: #{url}"
            Rails.logger.info '#####'

            # gets parsed page
            page = Nokogiri::HTML(open(url))

            # get skater and goalie tables
            skaters = page.css('#skaters').css('tbody')
            goalies = page.css('#goalies').css('tbody')

            team_goals = {}
            team_ppgs = {}
            team_shgs = {}
            team_evgs = {}
            team_shots = {}
            team_records = {}
            team_opponents = {}
            team_goals_against = {}

            skaters.css('tr').each do |skater_row|

                player = {
                    key: self.unique_id(skater_row.css('td')[1]),
                    name: skater_row.css('td')[1].content,
                    position: Enums::Position.parse(skater_row.css('td')[2].content),
                    team: Team.by_abbrev(skater_row.css('td')[3].content)
                }

                stats = {
                    home: (skater_row.css('td')[4].content != '@'),
                    opponent: skater_row.css('td')[5].content,
                    decision: Enums::Decision.parse(skater_row.css('td')[6].content),
                    goals: skater_row.css('td')[7].content,
                    assists: skater_row.css('td')[8].content,
                    points: skater_row.css('td')[9].content,
                    plusminus: skater_row.css('td')[10].content,
                    pim: skater_row.css('td')[11].content,
                    evg: skater_row.css('td')[12].content,
                    ppg: skater_row.css('td')[13].content,
                    shg: skater_row.css('td')[14].content,
                    gwg: skater_row.css('td')[15].content,
                    eva: skater_row.css('td')[16].content,
                    ppa: skater_row.css('td')[17].content,
                    sha: skater_row.css('td')[18].content,
                    shots: skater_row.css('td')[19].content,
                    shot_percentage: skater_row.css('td')[20].content,
                    shifts: skater_row.css('td')[21].content,
                    toi: skater_row.css('td')[22].attribute('csk').value
                }

                # store player stats
                #self.store_player player, stats, date

                # aggregate goals scored per team
                team_goals[player["team"]] += stats["goals"]
                team_ppgs[player["team"]] += stats["ppg"]
                team_shgs[player["team"]] += stats["shg"]
                team_evgs[player["team"]] += stats["evg"]
                team_shots[player["team"]] += stats["shots"]

                # collect unique teams
                if not team_records.contains player["team"]
                    team_records[player["team"]] = stats["decision"]
                    team_opponents[player["team"]] = stats["opponent"]
                end

            end

            team_records.each do |team, decision|
                team_goals_against[team] = team_goals[team_opponents[team]]
            end

            goalies.css('tr').each do |goalie_row|

                player = {
                    key: self.unique_id(goalie_row.css('td')[1]),
                    name: goalie_row.css('td')[1].content,
                    position: Enums::Position.parse(goalie_row.css('td')[2].content),
                    team: Team.by_abbrev(goalie_row.css('td')[3].content)
                }

                 stats = {
                    home: (goalie_row.css('td')[4].content != '@'),
                    opponent: goalie_row.css('td')[5].content,
                    decision: Enums::Decision.parse(goalie_row.css('td')[6].content),
                    verdict: Enums::GoalieRecord.parse(goalie_row.css('td')[7].content),
                    goals_against: goalie_row.css('td')[8].content,
                    shots_against: goalie_row.css('td')[9].content,
                    saves: goalie_row.css('td')[10].content,
                    save_percentage: goalie_row.css('td')[11].content,
                    shutout: goalie_row.css('td')[12].content,
                    pim: goalie_row.css('td')[13].content,
                    toi: goalie_row.css('td')[14].attribute('csk').value
                }

                # store goalie stats
                #self.store_player player, stats, date
            end

            Rails.logger.info "##### Pullers::DailyStats => Parsing complete"
            Rails.logger.info '#####'
        end

        def self.unique_id(row)

            # get unique id
            row.css('a').attribute('href').value.split('/').last[0..-6]
        end

        def self.store_player(info, stats, date)

            # find player for key
            player = Player.find_by key: info[:key]

            unless player

                # create new player if one was not found
                player = Player.create info

                Rails.logger.info "##### Pullers::DailyStats => Created #{info[:name]}"
            end

            # find opponent
            opponent = Team.by_abbrev stats.delete(:opponent)

            Rails.logger.info "(#{player.goalie?}) => #{stats}"

            # create stats record
            player.add_stats! opponent, stats, date
        end
    end
end
