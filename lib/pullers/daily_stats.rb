require 'open-uri'
require 'time'

module Pullers
    class DailyStats

        def self.run(date = Date.today - 2.day)

            Rails.logger.info "##### Pullers::DailyStats.run => Running for #{date}"

            url = "http://www.hockey-reference.com/friv/dailyleaders.cgi?month=#{date.month}&day=#{date.day}&year=#{date.year}"

            Rails.logger.info "##### Pullers::DailyStats.run => Opening: #{url}"

            page = Nokogiri::HTML(open(url))

            skaters = page.css('#skaters').css('tbody')
            goalies = page.css('#goalies').css('tbody')

            skater_hashes = []
            goalie_hashes = []

            skaters.css('tr').each do |skater_row|

                link_ids = skater_row.css('a').map { |link| link['href'] }
                unique_id = link_ids[0][11..-6]

                row = {
                    key: unique_id,
                    name: skater_row.css('td')[1].content,
                    position: Enums::Position.value_of skater_row.css('td')[2].content,
                    team: skater_row.css('td')[3].content,
                    home: (skater_row.css('td')[4].content != '@'),
                    opponent: skater_row.css('td')[5].content,
                    decision: Enums::Decision.parse skater_row.css('td')[6].content,
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

                # store_player row
            end

            puts skater_hashes[0]

            goalies.css('tr').each do |goalie_row|

                link_ids = goalie_row.css('a').map { |link| link['href'] }
                unique_id = link_ids[0][11..-6]

                 row = {
                    key: unique_id,
                    name: goalie_row.css('td')[1].content,
                    position: Enums::Position.value_of goalie_row.css('td')[2].content,
                    team: goalie_row.css('td')[3].content,
                    home: (goalie_row.css('td')[4].content != '@'),
                    opponent: goalie_row.css('td')[5].content,
                    decision: Enums::Decision.parse goalie_row.css('td')[6].content,
                    record: Enums::GoalieRecord.parse goalie_row.css('td')[7].content,
                    goals_against: goalie_row.css('td')[8].content,
                    shots_against: goalie_row.css('td')[9].content,
                    saves: goalie_row.css('td')[10].content,
                    save_percentage: goalie_row.css('td')[11].content,
                    shutout: goalie_row.css('td')[12].content,
                    pim: goalie_row.css('td')[13].content,
                    toi: goalie_row.css('td')[14].attribute('csk').value
                }

                # store_goalie row
            end

            Rails.logger.info "##### Pullers::DailyStats.run => Parsing complete"
        end

        def store_player(data)

            # Find player based on unique key
            player = Player.find_by key: data(:key)
            opponent = Team.find_by abbreviation: data(:opponent)

            player.add_stats!(opponent, data)
        end

        def store_goalie(data)

            # Find player based on name
        end
    end
end
